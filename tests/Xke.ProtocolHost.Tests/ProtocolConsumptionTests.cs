using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.ProtocolHost.Tests;

public sealed class ProtocolConsumptionTests
{
    [Fact]
    public async Task DesktopConsumesRealProtocolHttp_PersistsBothScopes_AndFallsBackWithoutCloudLogin()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        login.EnsureSuccessStatusCode();
        var desktopRoot = Path.Combine(host.Root, "desktop");
        var root = new XkeDataRoot(desktopRoot);
        root.Credentials().Save(new StoredCredentialDocument { User = "desktop-account", StudentId = "SERVER001", Password = "never-send-to-cloud" });
        root.ForStudent("SERVER001").SaveProfile(new StudentProfileDocument { StudentId = "SERVER001", Name = "Test Student" });
        var sessionId = root.Sessions().Create("desktop-account", "SERVER001", "auto").SessionId;

        var configDir = Path.Combine(host.Root, "开发数据", "课表服务");
        Directory.CreateDirectory(configDir);
        new ProtectedJsonFileStore().Write(Path.Combine(configDir, "key.json"), ProtocolTestHost.ApiKey);
        File.WriteAllText(Path.Combine(configDir, "client.local.json"), JsonSerializer.Serialize(new
        {
            provider = "protocol", endpoint = "https://protocol.example.test", protected_token_file = "key.json",
        }));
        var options = ScheduleApiOptions.Load(host.Root);
        using var transport = new LoopbackTransport(host.Client.BaseAddress!);
        using var http = new HttpClient(transport);
        var source = new ProtocolScheduleApiClient(http, options, TimeProvider.System);
        var direct = new DirectFallback();
        using var service = new ScheduleService(root, direct, new MobileSessionRuntimeStore(),
            new JwCaptchaChallengeStore(), new ManualCaptchaSolver(), snapshotSource: source);

        var request = new ScheduleRequest { Mode = "manual", ManualWeek = "1", AcademicTerm = "2026-2027-1" };
        var week = await service.FetchPreferredAsync(sessionId, request);
        Assert.Equal("protocol_api", week.Payload["retrieval_channel"]);
        Assert.Equal(0, direct.Calls);
        Assert.Equal(week.Payload["updated_at"], service.LoadLocal(sessionId, "第1周", "jw", request.AcademicTerm).Payload["updated_at"]);
        var semester = await service.FetchSemesterAsync(sessionId, request.AcademicTerm);
        Assert.NotEmpty(semester.Payload.Rows);
        Assert.True(service.LoadLocalSemester(sessionId, request.AcademicTerm).Payload.Cached);
        Assert.Equal(semester.Payload.UpdatedAt, service.LoadLocalSemester(sessionId, request.AcademicTerm).Payload.UpdatedAt);
        Assert.Equal(0, direct.Calls);

        host.School.Expired = true;
        var fallback = await service.FetchPreferredAsync(sessionId, request);
        Assert.Equal(1, direct.Calls);
        Assert.Equal("direct_jw", fallback.Payload["retrieval_channel"]);
        Assert.Equal("protocol_http_failure", fallback.Payload["fallback_reason"]);
        Assert.Equal(1, host.School.LoginSubmissions);
        Assert.All(transport.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.All(transport.Requests, request => Assert.StartsWith("/api/schedule/", request.Path));
    }

    private sealed class LoopbackTransport(Uri address) : DelegatingHandler(new HttpClientHandler())
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            Assert.Null(request.Content);
            request.RequestUri = new Uri(address, request.RequestUri.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class DirectFallback : IJwScheduleClient
    {
        public int Calls { get; private set; }
        public Task<JwScheduleFetchResult> FetchPreferredAsync(string user, string password, string preloginMode,
            ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null)
            => throw new InvalidOperationException("The retired mobile path must not run");
        public Task<JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode,
            ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null, string sessionId = "")
        {
            Calls++;
            return Task.FromResult(new TraditionalJwScheduleHtmlParser().Parse("""
                <table id="timetable"><tr><td>第1-2节 08:00-09:40</td><td><div class="kbcontent">Direct Course
                <font title="教师">Direct Teacher</font><font title="周次(节次)">1-16(周)[01-02节]</font><font title="教室">Direct Room</font></div></td></tr></table>
                """, request));
        }
    }
}
