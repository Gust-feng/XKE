using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;
using Xke.Security;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class AuthProtocolWebHostTests
{
    [Fact]
    public async Task DefaultProtocolEndpoints_RejectInvalidLoginAndUnknownChallengeWithoutRemoteCalls()
    {
        using var dataRoot = new TemporaryDirectory();
        using var webRoot = new TemporaryWebRoot();
        await using var service = new XkeWebHostService(new XkeWebHostOptions
        {
            EnableBackgroundTasks = false,
            ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(),
            AuthDataRootPath = dataRoot.RootPath,
            WebRootPath = webRoot.RootPath,
            Urls = new[] { "http://127.0.0.1:0" },
        });
        var launch = await service.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(launch.BaseUrl) };

        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            User = string.Empty,
            Password = "test-password",
        });
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        using var error = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(error.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, error.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(error.RootElement.GetProperty("detail").GetString(), error.RootElement.GetProperty("msg").GetString());

        using var captcha = await client.PostAsJsonAsync("/api/auth/captcha", new JwCaptchaSubmitRequest
        {
            ChallengeId = "unknown-challenge",
            CaptchaCode = "1234",
        });
        Assert.Equal(HttpStatusCode.Gone, captcha.StatusCode);
        Assert.Empty(Directory.EnumerateFiles(dataRoot.RootPath, "*.json.dpapi", SearchOption.AllDirectories));
    }
    [Theory]
    [InlineData(false, "20260001")]
    [InlineData(false, "test-login-alias")]
    [InlineData(true, "20260001")]
    public async Task DefaultProtocolLogin_CompletesInitializationOrRollsBackScheduleFailure(bool failSchedule, string loginUser)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var fakeSchedule = new DefaultFakeJwScheduleClient { CookieException = failSchedule ? new IOException("test-schedule-failure") : null };
        var handler = new ProtocolHandler();
        await using var service = new XkeWebHostService(new XkeWebHostOptions
        {
            ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(),
            EnableBackgroundTasks = false, AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath,
            Urls = new[] { "http://127.0.0.1:0" }, JwScheduleClient = fakeSchedule,
            JwLoginProtocolFactory = (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)),
        });
        var launch = await service.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(launch.BaseUrl) };
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { User = loginUser, Password = "test-password" });
        using var challenge = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("captcha_required", challenge.RootElement.GetProperty("data").GetProperty("status").GetString());
        var challengeId = challenge.RootElement.GetProperty("data").GetProperty("challenge_id").GetString()!;
        using var submit = await client.PostAsJsonAsync("/api/auth/captcha", new JwCaptchaSubmitRequest { ChallengeId = challengeId, CaptchaCode = "1234" });
        var root = new XkeDataRoot(data.RootPath);
        if (failSchedule)
        {
            Assert.Equal(HttpStatusCode.InternalServerError, submit.StatusCode);
            using var failure = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
            Assert.Equal("操作未完成，请重试", failure.RootElement.GetProperty("msg").GetString());
            Assert.DoesNotContain("test-schedule-failure", failure.RootElement.GetRawText());
            Assert.Null(root.Credentials().Load(loginUser));
            Assert.Equal(0, root.Sessions().Count());
            Assert.False(Directory.Exists(root.ForStudent("20260001").StudentDirectory));
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            Assert.Equal("20260001", root.Credentials().Load(loginUser)!.StudentId);
            Assert.Equal(1, root.Sessions().Count());
            using var payload = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
            var sid = payload.RootElement.GetProperty("data").GetProperty("session_id").GetString()!;
            using var session = await client.GetAsync("/api/auth/session?session_id=" + sid);
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            var protocolCalls = handler.Requests;
            using var restored = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { User = loginUser, Password = "test-password" });
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            Assert.Equal(protocolCalls, handler.Requests);
        }
    }

    [Theory]
    [InlineData("encrypted", false)]
    [InlineData("truncated", false)]
    [InlineData("missing-profile", false)]
    [InlineData("missing-schedule", false)]
    [InlineData("encrypted", true)]
    public async Task ProtocolLogin_RebuildsUnavailableWorkspaceAfterSchoolAuthentication(string state, bool failSchedule)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(data.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001", StudentId = "20260001", Password = "test-password", TrustedLoginToken = "existing-test-trust",
        });
        var student = root.ForStudent("20260001");
        var encrypted = new LocalEncryptedFileStore();
        encrypted.WriteJson(student.PreferencesPath, new AppSettingsDocument());
        switch (state)
        {
            case "encrypted":
                encrypted.WriteJson(student.ProfilePath, new StudentProfileDocument { StudentId = "20260001", Name = "旧测试资料" });
                break;
            case "truncated": File.WriteAllText(student.ProfilePath, "{\"student_id\":"); break;
            case "missing-schedule": student.SaveProfile(new StudentProfileDocument { StudentId = "20260001", Name = "测试同学" }); break;
        }
        var originalProfile = File.Exists(student.ProfilePath) ? File.ReadAllBytes(student.ProfilePath) : null;
        var originalPreferences = File.ReadAllBytes(student.PreferencesPath);
        var handler = new ProtocolHandler();
        await using var service = new XkeWebHostService(new XkeWebHostOptions
        {
            ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(), HostMode = "desktop-webview",
            DesktopRequestToken = "synthetic-protocol-desktop-capability",
            EnableBackgroundTasks = false, AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath,
            Urls = new[] { "http://127.0.0.1:0" },
            JwScheduleClient = new DefaultFakeJwScheduleClient { CookieException = failSchedule ? new JsonException("private-storage-diagnostic") : null },
            JwLoginProtocolFactory = (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)),
        });
        var launch = await service.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(launch.BaseUrl) };

        client.DefaultRequestHeaders.Add(LocalApiAccessPolicy.DesktopTokenHeaderName, "synthetic-protocol-desktop-capability");
        using var bootstrap = await client.PostAsJsonAsync("/api/desktop/auth-bootstrap", new { });
        Assert.Equal(HttpStatusCode.NotFound, bootstrap.StatusCode);
        Assert.Equal(0, handler.Requests);
        Assert.Equal(0, root.Sessions().Count());

        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { User = "20260001", Password = "test-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var challenge = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("captcha_required", challenge.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(originalPreferences, File.ReadAllBytes(student.PreferencesPath));
        Assert.Equal(originalProfile, File.Exists(student.ProfilePath) ? File.ReadAllBytes(student.ProfilePath) : null);
        Assert.Equal(0, root.Sessions().Count());

        using var submit = await client.PostAsJsonAsync("/api/auth/captcha", new JwCaptchaSubmitRequest
        {
            ChallengeId = challenge.RootElement.GetProperty("data").GetProperty("challenge_id").GetString()!, CaptchaCode = "1234",
        });
        Assert.Equal("existing-test-trust", root.Credentials().Load("20260001")!.TrustedLoginToken);
        if (failSchedule)
        {
            Assert.Equal(HttpStatusCode.InternalServerError, submit.StatusCode);
            Assert.DoesNotContain("private-storage-diagnostic", await submit.Content.ReadAsStringAsync());
            Assert.Equal(0, root.Sessions().Count());
            Assert.Equal(originalProfile, File.ReadAllBytes(student.ProfilePath));
            Assert.Equal(originalPreferences, File.ReadAllBytes(student.PreferencesPath));
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            Assert.Equal("测试同学", student.LoadProfile()!.Name);
            Assert.NotNull(student.LoadPreferences());
            Assert.Equal(1, root.Sessions().Count());
            using var profile = JsonDocument.Parse(File.ReadAllText(student.ProfilePath));
            using var preferences = JsonDocument.Parse(File.ReadAllText(student.PreferencesPath));
            using var payload = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
            using var session = await client.GetAsync("/api/auth/session?session_id=" + payload.RootElement.GetProperty("data").GetProperty("session_id").GetString());
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        }
    }

    private sealed class ProtocolHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/verifycode.servlet")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(uri.Query.Contains("flag=sess", StringComparison.Ordinal)
                    ? "abcdefghijklmnopqrstuvwxyz#11111111111111111111"
                    : "<table><tr><th>学号</th><td>20260001</td><th>姓名</th><td>测试同学</td></tr><tr><th>学院</th><td>测试学院</td><th>专业</th><td>软件</td></tr></table>"),
            };
            if (uri.AbsolutePath == "/Logon.do" && !uri.Query.Contains("flag=sess", StringComparison.Ordinal))
                response.Headers.Location = new Uri("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
            return Task.FromResult(response);
        }
    }

}
