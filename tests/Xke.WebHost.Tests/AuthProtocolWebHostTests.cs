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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reinitialize_ScheduleFailurePreservesTheCompleteWorkspaceAndActiveSession(bool unexpectedFailure)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var fake = new DefaultFakeJwScheduleClient();
        var handler = new ProtocolHandler();
        await using var host = await WebHostTestServer.StartAsync(web, data, hostMode: "desktop-webview",
            jwScheduleClient: fake,
            jwLoginProtocolFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)));
        var client = host.Client;
        var root = new XkeDataRoot(data.RootPath);

        async Task<HttpResponseMessage> Authenticate(string endpoint, string password)
        {
            using var begin = await client.PostAsJsonAsync(endpoint, new LoginRequest { User = "20260001", Password = password });
            Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
            using var challenge = JsonDocument.Parse(await begin.Content.ReadAsStringAsync());
            Assert.Equal("captcha_required", challenge.RootElement.GetProperty("data").GetProperty("status").GetString());
            return await client.PostAsJsonAsync("/api/auth/captcha", new JwCaptchaSubmitRequest
            {
                ChallengeId = challenge.RootElement.GetProperty("data").GetProperty("challenge_id").GetString()!,
                CaptchaCode = "1234",
            });
        }

        using var initial = await Authenticate("/api/auth/login", "old-test-password");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using var initialPayload = JsonDocument.Parse(await initial.Content.ReadAsStringAsync());
        var sessionId = initialPayload.RootElement.GetProperty("data").GetProperty("session_id").GetString()!;
        var student = root.ForStudent("20260001");
        student.SaveProfile(new StudentProfileDocument { StudentId = "20260001", Name = "原有资料", Nickname = "原有昵称" });
        student.SavePreferences(new AppSettingsDocument { Schedule = new ScheduleSettingsDocument { LessonReminderEnabled = true, LessonReminderMinutes = 45 } });
        var previousFiles = Directory.GetFiles(student.StudentDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(student.StudentDirectory, path), File.ReadAllBytes);
        var previousCredentials = JsonSerializer.Serialize(root.Credentials().Load("20260001"));
        var previousJwSession = JsonSerializer.Serialize(root.JwSessions().Load("20260001"));
        var sessionCount = root.Sessions().Count();
        fake.CookieException = unexpectedFailure ? new IOException("synthetic-write-failure") : new JwScheduleException("synthetic-school-failure", 502);

        using var failed = await Authenticate("/api/auth/reinitialize", "new-test-password");
        Assert.False(failed.IsSuccessStatusCode);
        Assert.Equal(2, fake.CookieCalls);
        Assert.Equal(previousCredentials, JsonSerializer.Serialize(root.Credentials().Load("20260001")));
        Assert.Equal(previousJwSession, JsonSerializer.Serialize(root.JwSessions().Load("20260001")));
        Assert.Equal(sessionCount, root.Sessions().Count());
        var restoredFiles = Directory.GetFiles(student.StudentDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(student.StudentDirectory, path), File.ReadAllBytes);
        Assert.Equal(previousFiles.Keys.Order(), restoredFiles.Keys.Order());
        foreach (var (path, bytes) in previousFiles) Assert.Equal(bytes, restoredFiles[path]);

        // Check the active session before any session read could restore it as a side effect.
        var protocolRequests = handler.Requests;
        using var bootstrap = await client.PostAsJsonAsync("/api/desktop/auth-bootstrap", new { });
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        using var bootstrapPayload = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync());
        Assert.Equal(sessionId, bootstrapPayload.RootElement.GetProperty("data").GetProperty("auth").GetProperty("session_id").GetString());
        using var session = await client.GetAsync("/api/auth/session?session_id=" + sessionId);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        using var sessionPayload = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        Assert.Equal("原有资料", sessionPayload.RootElement.GetProperty("data").GetProperty("student").GetProperty("name").GetString());
        Assert.Equal("local_cache", sessionPayload.RootElement.GetProperty("data").GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        using var trusted = await client.PostAsJsonAsync("/api/auth/trusted-login", new TrustedLoginRequest
        {
            User = "20260001", TrustedLoginToken = root.Credentials().Load("20260001")!.TrustedLoginToken,
        });
        Assert.Equal(HttpStatusCode.OK, trusted.StatusCode);
        Assert.Equal(protocolRequests, handler.Requests);
        Assert.Equal(2, fake.CookieCalls);
    }

    [Theory]
    [InlineData("自选昵称", true)]
    [InlineData("", false)]
    public async Task Reinitialize_PreservesLocalDisplayPreferenceWhileRefreshingSchoolProfile(string nickname, bool useNickname)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var handler = new ProtocolHandler();
        await using var host = await WebHostTestServer.StartAsync(web, data, jwScheduleClient: new DefaultFakeJwScheduleClient(),
            jwLoginProtocolFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)));
        async Task<JsonDocument> Authenticate(string endpoint)
        {
            using var begin = await host.Client.PostAsJsonAsync(endpoint, new LoginRequest { User = "20260001", Password = "test-password" });
            Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
            using var challenge = JsonDocument.Parse(await begin.Content.ReadAsStringAsync());
            using var submit = await host.Client.PostAsJsonAsync("/api/auth/captcha", new JwCaptchaSubmitRequest
            {
                ChallengeId = challenge.RootElement.GetProperty("data").GetProperty("challenge_id").GetString()!, CaptchaCode = "1234",
            });
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            return JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
        }
        using var first = await Authenticate("/api/auth/login");
        var sessionId = first.RootElement.GetProperty("data").GetProperty("session_id").GetString()!;
        using var update = await host.Client.PostAsJsonAsync("/api/auth/profile?session_id=" + sessionId,
            new ProfileRequest { Nickname = nickname, UseNickname = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var renewed = await Authenticate("/api/auth/reinitialize");
        var student = renewed.RootElement.GetProperty("data").GetProperty("student");
        Assert.Equal("测试同学", student.GetProperty("name").GetString());
        Assert.Equal(nickname, student.GetProperty("nickname").GetString());
        Assert.Equal(useNickname, student.GetProperty("use_nickname").GetBoolean());
        var saved = new XkeDataRoot(data.RootPath).ForStudent("20260001").LoadProfile()!;
        Assert.Equal(nickname, saved.Nickname);
        Assert.Equal(useNickname, saved.UseNickname);
        Assert.Equal("信息工程学院", saved.AcademyName);
        Assert.Equal("软件技术", saved.MajorName);
    }

}
