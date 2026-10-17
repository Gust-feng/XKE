using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Schedule;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleSourceIntegrationTests
{
    [Fact]
    public async Task ApiSuccess_NeverCallsJwAndPersistsTheUnifiedPayload()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var fake = new DefaultFakeJwScheduleClient { CookieException = new IOException("JW should not be called") };
        await using var app = Create(data, web, fake, PersonalScheduleApiClientTests.Create(new PersonalScheduleApiClientTests.SnapshotHandler()));
        var auth = app.Services.GetRequiredService<AuthService>();
        var sessionId = Seed(data, auth);
        var service = app.Services.GetRequiredService<ScheduleService>();
        var result = await service.FetchPreferredAsync(sessionId, new ScheduleRequest(), default);
        Assert.Equal("jw_schedule", result.Payload["source_key"]);
        var typed = JsonSerializer.Deserialize<Xke.Contracts.Responses.ScheduleData>(JsonSerializer.Serialize(result.Payload))!;
        Assert.Equal("jw_schedule", typed.SourceKey);
        Assert.Equal("personal_api", typed.RetrievalChannel);
        Assert.NotEmpty(typed.Rows);
        Assert.Equal("personal_api", result.Payload["retrieval_channel"]);
        Assert.Equal("live_fetch", result.Payload["data_origin"]);
        var cached = service.LoadLocal(sessionId, "第8周", "preferred");
        Assert.Equal("personal_api", cached.Payload["retrieval_channel"]);
        var typedCache = JsonSerializer.Deserialize<Xke.Contracts.Responses.ScheduleData>(JsonSerializer.Serialize(cached.Payload))!;
        Assert.Equal("jw_schedule", typedCache.SourceKey);
        Assert.True(typedCache.Cached);
        Assert.Equal(result.Payload["updated_at"], cached.Payload["updated_at"]);
        Assert.Equal(0, fake.CookieCalls);
        var student = new XkeDataRoot(data.RootPath).ForStudent("20260001");
        student.SavePreferences(AuthSettingsCatalog.Apply(student.LoadPreferences()!, new PreferencesRequest { Schedule = new() { AutoRefreshEnabled = true } }));
        await service.RefreshInBackgroundAsync(sessionId, default);
        Assert.Equal(0, fake.CookieCalls);
    }

    [Fact]
    public async Task PersonalApiWithoutAccountBinding_UsesCurrentLocalProfileAndUnifiedCache()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var body = PersonalScheduleApiClientTests.SnapshotHandler.Snapshot();
        body.Remove("student_id");
        body.Remove("academic_term");
        body.Remove("term_label");
        var source = new PersonalScheduleApiClient(new HttpClient(new PersonalScheduleApiClientTests.SnapshotHandler { Body = body }),
            new ScheduleApiOptions { Endpoint = "https://example.test/schedule" }, new PersonalScheduleApiClientTests.SnapshotClock());
        var fake = new DefaultFakeJwScheduleClient { CookieException = new IOException("JW must not be called") };
        await using var app = Create(data, web, fake, source);
        var sessionId = Seed(data, app.Services.GetRequiredService<AuthService>());
        var service = app.Services.GetRequiredService<ScheduleService>();

        var result = await service.FetchPreferredAsync(sessionId, new ScheduleRequest(), default);
        var student = Assert.IsType<Dictionary<string, object?>>(result.Payload["student"]);
        Assert.Equal("20260001", student["student_id"]);
        Assert.Equal("测试同学", student["name"]);
        Assert.Equal("personal_api", result.Payload["retrieval_channel"]);
        Assert.Equal("personal_api", service.LoadLocal(sessionId, "第8周", "preferred").Payload["retrieval_channel"]);
        Assert.Equal(0, fake.CookieCalls);
    }

    [Fact]
    public async Task ApiFailure_FallsBackToJwWithTheSameDataShape()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var fake = new DefaultFakeJwScheduleClient();
        var handler = new PersonalScheduleApiClientTests.SnapshotHandler { Status = System.Net.HttpStatusCode.ServiceUnavailable };
        await using var app = Create(data, web, fake, PersonalScheduleApiClientTests.Create(handler));
        var sessionId = Seed(data, app.Services.GetRequiredService<AuthService>());
        var service = app.Services.GetRequiredService<ScheduleService>();
        var direct = await service.FetchPreferredAsync(sessionId, new ScheduleRequest(), default);
        Assert.Equal(1, fake.CookieCalls);
        Assert.Equal("snapshot_http_failure", direct.Payload["fallback_reason"]);
        Assert.Equal("direct_jw", direct.Payload["retrieval_channel"]);
        Assert.Equal("jw_schedule", direct.Payload["source_key"]);
        var typed = JsonSerializer.Deserialize<Xke.Contracts.Responses.ScheduleData>(JsonSerializer.Serialize(direct.Payload))!;
        Assert.Equal("jw_schedule", typed.SourceKey);
        Assert.Equal("direct_jw", typed.RetrievalChannel);
        Assert.True(direct.Payload.ContainsKey("rows"));
        Assert.True(direct.Payload.ContainsKey("week_dates"));
        Assert.True(direct.Payload.ContainsKey("term"));
        Assert.True(direct.Payload.ContainsKey("student"));
        using var cloudApp = Create(data, web, fake, PersonalScheduleApiClientTests.Create(new PersonalScheduleApiClientTests.SnapshotHandler()));
        var cloudService = cloudApp.Services.GetRequiredService<ScheduleService>();
        var cloudSid = cloudApp.Services.GetRequiredService<AuthService>().DesktopBootstrap().Payload.SessionId;
        var cloud = await cloudService.FetchPreferredAsync(cloudSid, new ScheduleRequest(), default);
        Assert.Equal(direct.Payload.Keys.Order().ToArray(), cloud.Payload.Keys.Order().ToArray());
    }

    [Fact]
    public async Task EmptySuccessfulSnapshot_PreservesLocalSessionAndWorkspaceRecovery()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var body = PersonalScheduleApiClientTests.SnapshotHandler.Snapshot();
        body["courses"] = Array.Empty<object>();
        await using var app = Create(data, web, new DefaultFakeJwScheduleClient(), PersonalScheduleApiClientTests.Create(new PersonalScheduleApiClientTests.SnapshotHandler { Body = body }));
        var auth = app.Services.GetRequiredService<AuthService>();
        var sid = Seed(data, auth);
        await app.Services.GetRequiredService<ScheduleService>().FetchPreferredAsync(sid, new ScheduleRequest(), default);
        Assert.NotNull(auth.GetSession(sid).Payload);
        Assert.NotNull(auth.TrustedLogin(new TrustedLoginRequest { User = "20260001", TrustedLoginToken = "test-trust" }).Payload);
    }

    [Fact]
    public async Task OlderApiSnapshot_DoesNotOverwriteNewerDirectCache()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        await using var app = Create(data, web, new DefaultFakeJwScheduleClient(), PersonalScheduleApiClientTests.Create(new PersonalScheduleApiClientTests.SnapshotHandler()));
        var sessionId = Seed(data, app.Services.GetRequiredService<AuthService>(), "2026-10-07T00:30:00Z");
        var service = app.Services.GetRequiredService<ScheduleService>();
        var result = await service.FetchPreferredAsync(sessionId, new ScheduleRequest(), default);
        Assert.True((bool)result.Payload["cached"]!);
        Assert.Equal("direct_jw", result.Payload["retrieval_channel"]);
        Assert.Equal("2026-10-07T00:30:00Z", result.Payload["updated_at"]);
    }

    [Fact]
    public async Task ApiFailure_WithExpiredJwStillReturnsTheExistingCaptchaFlow()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var fake = new DefaultFakeJwScheduleClient { CookieException = new JwCaptchaRequiredException("captcha needed", "data:image/png;base64,AA==", 120) };
        await using var app = Create(data, web, fake, new MissingSnapshot());
        var sessionId = Seed(data, app.Services.GetRequiredService<AuthService>());
        var service = app.Services.GetRequiredService<ScheduleService>();
        var result = await service.FetchPreferredAsync(sessionId, new ScheduleRequest(), default);
        Assert.Equal("captcha_required", result.Payload["status"]);
        Assert.NotNull(service.LoadLocal(sessionId, "第8周", "preferred").Payload);
    }

    [Fact]
    public async Task PrivateConfig_IsNeverExposedToFrontendConfig()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath, EnableBackgroundTasks = false,
            Urls = new[] { "http://127.0.0.1:0" }, ScheduleSnapshotSource = new MissingSnapshot(),
            ScheduleApiOptions = new ScheduleApiOptions { Endpoint = "https://example.test/private-service", BearerToken = "private-test-token", StudentId = "20260001" },
        });
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(((Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature)app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features[typeof(Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature)]!).Addresses.First()) };
        var json = await client.GetStringAsync("/api/config");
        Assert.DoesNotContain("bearer_token", json);
        Assert.DoesNotContain("private-test-token", json);
        Assert.DoesNotContain("private-service", json);
        await app.StopAsync();
    }

    [Theory]
    [InlineData("mobile")]
    [InlineData("encrypted")]
    [InlineData("mobile_schedule")]
    [InlineData("unknown")]
    public async Task LocalRead_RejectsUnsupportedSourcesWithABusinessError(string source)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        await using var app = Create(data, web, new(), new MissingSnapshot());
        var auth = app.Services.GetRequiredService<AuthService>();
        var session = Seed(data, auth);
        var error = Assert.Throws<ScheduleException>(() => app.Services.GetRequiredService<ScheduleService>().LoadLocal(session, "", source));
        Assert.Equal(400, error.StatusCode);
        Assert.NotNull(auth.GetSession(session).Payload);

        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        var addresses = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!;
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(addresses.Addresses)) };
        client.DefaultRequestHeaders.Add("X-XKE-Session", session);
        using var response = await client.GetAsync("/api/schedule/local?source=" + Uri.EscapeDataString(source));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("不支持的课表来源", body.RootElement.GetProperty("msg").GetString());
        await app.StopAsync();
    }

    [Fact]
    public void Mapper_DefaultSourceMetadataUsesTheCurrentProtocol()
    {
        var result = ScheduleDataMapper.NormalizeWeek(new() { ["rows"] = Array.Empty<object>() },
            new StudentProfileDocument { StudentId = "20260001" }, true, "local_cache", "jw_schedule", "", "", "", null);
        Assert.Equal("jw_schedule", result["source_key"]);
        Assert.Equal("jw", result["source_type"]);
        Assert.Equal("教务系统课表", result["source_label"]);
        Assert.Equal("direct_jw", result["retrieval_channel"]);
    }

    private static WebApplication Create(TemporaryDirectory data, TemporaryWebRoot web, DefaultFakeJwScheduleClient fake, IScheduleSnapshotSource source)
        => XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath, EnableBackgroundTasks = false,
            ScheduleSnapshotSource = source, ScheduleApiOptions = new ScheduleApiOptions(), JwScheduleClient = fake,
        });

    private static string Seed(TemporaryDirectory data, AuthService auth, string fetchedAt = "2026-10-06T00:00:00Z")
    {
        var root = new XkeDataRoot(data.RootPath);
        root.Credentials().Save(new StoredCredentialDocument { User = "20260001", StudentId = "20260001", Password = "test-password", TrustedLoginToken = "test-trust" });
        var student = root.ForStudent("20260001");
        student.SaveProfile(new StudentProfileDocument { StudentId = "20260001", Name = "测试同学" });
        student.SavePreferences(AuthSettingsCatalog.CreateDefault());
        student.Schedules().SaveSourceSchedule(new Dictionary<string, object?>
        {
            ["source_key"] = "jw_schedule", ["week_label"] = "第8周", ["updated_at"] = fetchedAt, ["retrieval_channel"] = "direct_jw",
            ["term"] = new Dictionary<string, object?> { ["academic_term"] = "test-term", ["label"] = "测试学期" },
            ["rows"] = new List<Dictionary<string, object?>> { new() { ["course_name"] = "本地课程", ["weekday"] = "星期一", ["section"] = "1-2" } },
        }, new(), "jw_schedule", "第8周");
        return auth.DesktopBootstrap().Payload.SessionId;
    }

    private sealed class MissingSnapshot : IScheduleSnapshotSource
    {
        public Task<ScheduleSnapshotAttempt> FetchAsync(string studentId, ScheduleRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ScheduleSnapshotAttempt.Unavailable("snapshot_unavailable"));
    }
}
