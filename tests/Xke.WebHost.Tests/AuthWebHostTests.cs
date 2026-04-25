using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class AuthWebHostTests
{
    [Fact]
    public async Task AuthEndpoints_SupportLoginSessionPreferencesProfileAvatarAndLogout()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-data");
        var fakeJw = new FakeJwAuthClient();
        var fakeSchedule = new DefaultFakeJwScheduleClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "coded",
        });
        var loginDoc = await ReadJsonAsync(login);
        var loginData = GetData(loginDoc);
        var sessionId = loginData.GetProperty("session_id").GetString();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(loginDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(1, fakeJw.InitializeCalls);
        Assert.NotNull(sessionId);
        Assert.True(loginData.GetProperty("connected").GetBoolean());
        Assert.Equal("auto", loginData.GetProperty("prelogin_mode").GetString());
        Assert.Equal("20260001", loginData.GetProperty("student").GetProperty("student_id").GetString());
        Assert.Equal("live_fetch", loginData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal("第7周", loginData.GetProperty("cached_schedule").GetProperty("week_label").GetString());
        Assert.Equal(1, fakeSchedule.PreferredCalls);

        var localLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var localDoc = await ReadJsonAsync(localLogin);
        Assert.Equal(HttpStatusCode.OK, localLogin.StatusCode);
        Assert.False(GetData(localDoc).GetProperty("connected").GetBoolean());
        Assert.Equal("local_cache", GetData(localDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal(1, fakeJw.InitializeCalls);

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var sessionResponse = await client.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.False(GetData(sessionDoc).GetProperty("connected").GetBoolean());
        Assert.Equal("local_cache", GetData(sessionDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());

        var preferencesResponse = await client.PostAsJsonAsync($"/api/auth/preferences?session_id={sessionId}", new
        {
            appearance = new { theme = "forest-notes" },
            auth = new { prelogin_mode = "capture" },
        });
        var preferencesDoc = await ReadJsonAsync(preferencesResponse);
        Assert.Equal(HttpStatusCode.OK, preferencesResponse.StatusCode);
        Assert.Equal("forest-notes", GetData(preferencesDoc).GetProperty("settings").GetProperty("appearance").GetProperty("theme").GetString());
        Assert.Equal("auto", GetData(preferencesDoc).GetProperty("settings").GetProperty("auth").GetProperty("prelogin_mode").GetString());

        var profileResponse = await client.PostAsJsonAsync($"/api/auth/profile?session_id={sessionId}", new
        {
            nickname = "阿科",
            use_nickname = true,
        });
        var profileDoc = await ReadJsonAsync(profileResponse);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.Equal("阿科", GetData(profileDoc).GetProperty("student").GetProperty("nickname").GetString());

        using var multipart = new MultipartFormDataContent();
        var avatarContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 });
        avatarContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(avatarContent, "file", "avatar.png");
        using var avatarUploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/avatar");
        avatarUploadRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        avatarUploadRequest.Content = multipart;
        var avatarUpload = await client.SendAsync(avatarUploadRequest);
        var avatarUploadDoc = await ReadJsonAsync(avatarUpload);
        Assert.Equal(HttpStatusCode.OK, avatarUpload.StatusCode);
        Assert.Contains("/api/auth/avatar?v=", GetData(avatarUploadDoc).GetProperty("avatar_url").GetString());

        using var avatarRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/avatar");
        avatarRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var avatarResponse = await client.SendAsync(avatarRequest);
        Assert.Equal(HttpStatusCode.OK, avatarResponse.StatusCode);
        Assert.Equal("image/png", avatarResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await avatarResponse.Content.ReadAsByteArrayAsync());

        var logout = await client.PostAsync($"/api/auth/logout?session_id={sessionId}", null);
        var logoutDoc = await ReadJsonAsync(logout);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal("已注销", logoutDoc.RootElement.GetProperty("msg").GetString());

        using var expiredRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        expiredRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var expiredResponse = await client.SendAsync(expiredRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
    }

    [Fact]
    public async Task AuthEndpoints_SupportTrustedLoginAndExplicitConnect()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-connect");
        var fakeJw = new FakeJwAuthClient();
        var fakeSchedule = new DefaultFakeJwScheduleClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);
        var trustedToken = GetData(loginDoc).GetProperty("trusted_login_token").GetString();

        var trustedLogin = await client.PostAsJsonAsync("/api/auth/trusted-login", new
        {
            user = "20260001",
            prelogin_mode = "capture",
            trusted_login_token = trustedToken,
        });
        var trustedDoc = await ReadJsonAsync(trustedLogin);
        var sessionId = GetData(trustedDoc).GetProperty("session_id").GetString();

        Assert.Equal(HttpStatusCode.OK, trustedLogin.StatusCode);
        Assert.False(GetData(trustedDoc).GetProperty("connected").GetBoolean());
        Assert.Equal("local_cache", GetData(trustedDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());

        using var connectRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/connect");
        connectRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        connectRequest.Content = JsonContent.Create(new { prelogin_mode = "coded" });
        var connectResponse = await client.SendAsync(connectRequest);
        var connectDoc = await ReadJsonAsync(connectResponse);

        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);
        Assert.True(GetData(connectDoc).GetProperty("connected").GetBoolean());
        Assert.Equal(1, fakeJw.ConnectCalls);
        Assert.Equal("auto", GetData(connectDoc).GetProperty("prelogin_mode").GetString());
        Assert.Equal("local_cache", GetData(connectDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());

        var badTrustedLogin = await client.PostAsJsonAsync("/api/auth/trusted-login", new
        {
            user = "20260001",
            prelogin_mode = "capture",
            trusted_login_token = "bad-token",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, badTrustedLogin.StatusCode);
    }

    [Fact]
    public async Task AuthLogin_FirstInitializationFailsWhenScheduleBootstrapFailsAndDoesNotKeepLocalState()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-init-fail");
        var fakeJw = new FakeJwAuthClient();
        var fakeSchedule = new DefaultFakeJwScheduleClient
        {
            FailPreferred = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);

        Assert.Equal(HttpStatusCode.InternalServerError, login.StatusCode);
        Assert.Equal("首次初始化未完成：移动教务课表初始化失败", loginDoc.RootElement.GetProperty("detail").GetString());

        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        Assert.False(root.Credentials().Exists("20260001"));
        Assert.Equal(0, root.Sessions().Count());
        Assert.False(Directory.Exists(root.ForStudent("20260001").StudentDirectory));
    }

    [Fact]
    public async Task AuthLogin_FirstInitializationRollsBackUnexpectedScheduleExceptions()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-init-unexpected-fail");
        var fakeSchedule = new DefaultFakeJwScheduleClient
        {
            PreferredException = new InvalidOperationException("模拟课表落库异常"),
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);

        Assert.Equal(HttpStatusCode.InternalServerError, login.StatusCode);
        Assert.Equal("首次初始化未完成：本地初始化失败：模拟课表落库异常", loginDoc.RootElement.GetProperty("detail").GetString());

        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        Assert.False(root.Credentials().Exists("20260001"));
        Assert.Equal(0, root.Sessions().Count());
        Assert.False(Directory.Exists(root.ForStudent("20260001").StudentDirectory));
    }

    [Fact]
    public async Task AuthLogin_FirstInitializationRequiresMobileTokenAndDoesNotKeepLocalState()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-token-fail");
        var fakeJw = new FakeJwAuthClient
        {
            MissingInitializeToken = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);

        Assert.Equal(HttpStatusCode.BadGateway, login.StatusCode);
        Assert.Equal("首次初始化未完成：移动教务未返回有效 Token", loginDoc.RootElement.GetProperty("detail").GetString());

        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        Assert.False(root.Credentials().Exists("20260001"));
        Assert.Equal(0, root.Sessions().Count());
        Assert.False(Directory.Exists(root.ForStudent("20260001").StudentDirectory));
    }

    [Fact]
    public async Task AuthLogin_ExistingCredentialRequiresCompleteLocalProfileAndScheduleWithoutCallingRemote()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-local-incomplete");
        var fakeJw = new FakeJwAuthClient();
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001",
            Password = "secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-local-token",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
        root.ForStudent("20260001").SaveProfile(new StudentProfileDocument
        {
            StudentId = "20260001",
            Name = "测试同学",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);

        Assert.Equal(HttpStatusCode.Conflict, login.StatusCode);
        Assert.Contains("本地初始化数据不完整", loginDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, fakeJw.InitializeCalls);
        Assert.Equal(0, root.Sessions().Count());

        var trustedLogin = await client.PostAsJsonAsync("/api/auth/trusted-login", new
        {
            user = "20260001",
            prelogin_mode = "capture",
            trusted_login_token = "trusted-local-token",
        });
        var trustedDoc = await ReadJsonAsync(trustedLogin);

        Assert.Equal(HttpStatusCode.Conflict, trustedLogin.StatusCode);
        Assert.Contains("本地初始化数据不完整", trustedDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, root.Sessions().Count());
    }

    [Fact]
    public async Task AuthReinitialize_RepairsIncompleteLocalStateAndKeepsTrustedToken()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-reinit");
        var fakeJw = new FakeJwAuthClient();
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001",
            Password = "old-secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-local-token",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
        root.ForStudent("20260001").SaveProfile(new StudentProfileDocument
        {
            StudentId = "20260001",
            Name = "旧资料",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;

        var reinitialize = await client.PostAsJsonAsync("/api/auth/reinitialize", new
        {
            user = "20260001",
            password = "new-secret",
            prelogin_mode = "coded",
        });
        var reinitializeDoc = await ReadJsonAsync(reinitialize);
        var reinitializeData = GetData(reinitializeDoc);

        Assert.Equal(HttpStatusCode.OK, reinitialize.StatusCode);
        Assert.Equal("重新初始化成功，已刷新本地可信状态", reinitializeDoc.RootElement.GetProperty("msg").GetString());
        Assert.True(reinitializeData.GetProperty("connected").GetBoolean());
        Assert.Equal("trusted-local-token", reinitializeData.GetProperty("trusted_login_token").GetString());
        Assert.Equal("live_fetch", reinitializeData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal(1, fakeJw.InitializeCalls);

        var trustedLogin = await client.PostAsJsonAsync("/api/auth/trusted-login", new
        {
            user = "20260001",
            prelogin_mode = "capture",
            trusted_login_token = "trusted-local-token",
        });
        var trustedDoc = await ReadJsonAsync(trustedLogin);

        Assert.Equal(HttpStatusCode.OK, trustedLogin.StatusCode);
        Assert.Equal("local_cache", GetData(trustedDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal("new-secret", root.Credentials().Load("20260001")?.Password);
    }

    [Fact]
    public async Task AuthReinitialize_FailureKeepsPreviousCredentialAndDoesNotCreateSession()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-reinit-fail");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001",
            Password = "old-secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-local-token",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient { FailPreferred = true });
        var client = host.Client;

        var reinitialize = await client.PostAsJsonAsync("/api/auth/reinitialize", new
        {
            user = "20260001",
            password = "new-secret",
            prelogin_mode = "capture",
        });
        var reinitializeDoc = await ReadJsonAsync(reinitialize);

        Assert.Equal(HttpStatusCode.InternalServerError, reinitialize.StatusCode);
        Assert.Equal("重新初始化未完成：移动教务课表初始化失败", reinitializeDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal("old-secret", root.Credentials().Load("20260001")?.Password);
        Assert.Equal("trusted-local-token", root.Credentials().Load("20260001")?.TrustedLoginToken);
        Assert.Equal(0, root.Sessions().Count());
    }

    [Fact]
    public async Task AuthReinitialize_FailureRestoresExistingTrustedWorkspaceSnapshot()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-reinit-snapshot");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);

        await using (var initialHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient()))
        {
            var initialClient = initialHost.Client;
            var login = await initialClient.PostAsJsonAsync("/api/auth/login", new
            {
                user = "20260001",
                password = "old-secret",
                prelogin_mode = "capture",
            });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        var originalToken = root.Credentials().Load("20260001")?.TrustedLoginToken;
        Assert.False(string.IsNullOrWhiteSpace(originalToken));
        Assert.Equal("测试同学", root.ForStudent("20260001").LoadProfile()?.Name);
        var sessionCountBeforeRepair = root.Sessions().Count();

        await using var failingHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient { FailPreferred = true });
        var client = failingHost.Client;

        var reinitialize = await client.PostAsJsonAsync("/api/auth/reinitialize", new
        {
            user = "20260001",
            password = "new-secret",
            prelogin_mode = "coded",
        });
        var reinitializeDoc = await ReadJsonAsync(reinitialize);

        Assert.Equal(HttpStatusCode.InternalServerError, reinitialize.StatusCode);
        Assert.Equal("重新初始化未完成：移动教务课表初始化失败", reinitializeDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal("old-secret", root.Credentials().Load("20260001")?.Password);
        Assert.Equal(originalToken, root.Credentials().Load("20260001")?.TrustedLoginToken);
        Assert.Equal("测试同学", root.ForStudent("20260001").LoadProfile()?.Name);
        Assert.Equal(sessionCountBeforeRepair, root.Sessions().Count());

        var trustedLogin = await client.PostAsJsonAsync("/api/auth/trusted-login", new
        {
            user = "20260001",
            prelogin_mode = "capture",
            trusted_login_token = originalToken,
        });
        var trustedDoc = await ReadJsonAsync(trustedLogin);

        Assert.Equal(HttpStatusCode.OK, trustedLogin.StatusCode);
        Assert.Equal("local_cache", GetData(trustedDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal("测试同学", GetData(trustedDoc).GetProperty("student").GetProperty("name").GetString());
    }
    [Fact]
    public async Task AuthSession_IncompleteLocalStateFailsInsteadOfReturningEmptyWorkspace()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-session-incomplete");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001",
            Password = "secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-local-token",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
        var sessionId = root.Sessions().Create("20260001", "20260001", "capture").SessionId;

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;
        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);

        var sessionResponse = await client.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);

        Assert.Equal(HttpStatusCode.Conflict, sessionResponse.StatusCode);
        Assert.Contains("本地初始化数据不完整", sessionDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(1, root.Sessions().Count());
    }

    [Fact]
    public async Task AuthConnect_RemoteFailureKeepsLocalSessionAndCachedStateUsable()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-connect-fail");
        var fakeJw = new FakeJwAuthClient
        {
            FailConnect = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var sessionId = GetData(await ReadJsonAsync(login)).GetProperty("session_id").GetString();

        using var connectRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/connect");
        connectRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        connectRequest.Content = JsonContent.Create(new { prelogin_mode = "capture" });
        var connectResponse = await client.SendAsync(connectRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, connectResponse.StatusCode);

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var sessionResponse = await client.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.False(GetData(sessionDoc).GetProperty("connected").GetBoolean());
        Assert.Equal("local_cache", GetData(sessionDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.True(rootExists(dataRoot.RootPath, "20260001"));

        static bool rootExists(string dataRootPath, string studentId)
        {
            var root = new XkeDataRoot(dataRootPath, dataRootPath);
            return root.Credentials().Exists(studentId) && root.Sessions().Count() > 0;
        }
    }

    [Fact]
    public async Task AuthConnect_IdentityMismatchKeepsLocalSessionAndCachedStateUsable()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-connect-mismatch");
        var fakeJw = new FakeJwAuthClient
        {
            ConnectStudentIdOverride = "20269999",
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwAuthClient: fakeJw,
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var sessionId = GetData(await ReadJsonAsync(login)).GetProperty("session_id").GetString();

        using var connectRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/connect");
        connectRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        connectRequest.Content = JsonContent.Create(new { prelogin_mode = "capture" });
        var connectResponse = await client.SendAsync(connectRequest);
        var connectDoc = await ReadJsonAsync(connectResponse);

        Assert.Equal(HttpStatusCode.Conflict, connectResponse.StatusCode);
        Assert.Contains("身份与本地初始化账号不一致", connectDoc.RootElement.GetProperty("detail").GetString());

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var sessionResponse = await client.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.Equal("20260001", GetData(sessionDoc).GetProperty("student").GetProperty("student_id").GetString());
        Assert.Equal("local_cache", GetData(sessionDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.False(Directory.Exists(new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath).ForStudent("20269999").StudentDirectory));
    }

    private static JsonElement GetData(JsonDocument document)
    {
        return document.RootElement.GetProperty("data");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    private sealed class FakeJwAuthClient : IJwAuthClient
    {
        public bool MissingInitializeToken { get; init; }
        public bool MissingInitializeProfile { get; init; }
        public bool FailConnect { get; init; }
        public string ConnectStudentIdOverride { get; init; } = string.Empty;
        public int InitializeCalls { get; private set; }
        public int ConnectCalls { get; private set; }

        public Task<JwAuthResult> InitializeAsync(string user, string password, string preloginMode, CancellationToken cancellationToken = default, MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            InitializeCalls++;
            var profile = MissingInitializeProfile ? new StudentProfileDocument() : BuildProfile(user, name: "测试同学");
            var token = MissingInitializeToken ? string.Empty : "mobile-token-initial";
            return Task.FromResult(new JwAuthResult(profile, token));
        }

        public Task<JwAuthResult> ConnectAsync(string user, string password, string preloginMode, CancellationToken cancellationToken = default, MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            ConnectCalls++;
            if (FailConnect)
            {
                throw new JwAuthException("移动教务验证失败：上游暂不可用");
            }

            var profileUser = string.IsNullOrWhiteSpace(ConnectStudentIdOverride) ? user : ConnectStudentIdOverride;
            return Task.FromResult(new JwAuthResult(BuildProfile(profileUser, name: "测试同学-已连接"), "mobile-token-connected"));
        }

        private static StudentProfileDocument BuildProfile(string user, string name)
        {
            return new StudentProfileDocument
            {
                StudentId = user,
                Name = name,
                ClassName = "软件一班",
                MajorName = "软件技术",
                AcademyName = "信息工程学院",
                TrainingLevel = "专科",
                CampusName = "主校区",
                UpdatedAt = "2026-04-23T12:00:00Z",
            };
        }
    }

}
