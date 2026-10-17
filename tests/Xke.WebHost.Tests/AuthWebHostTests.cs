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
        var fakeSchedule = new DefaultFakeJwScheduleClient();
        var handler = new ProtocolHandler();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwScheduleClient: fakeSchedule,
            jwLoginProtocolFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)));
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "coded",
        });
        var loginDoc = await ReadJsonAsync(login);
        var challengeId = GetData(loginDoc).GetProperty("challenge_id").GetString();
        var submit = await client.PostAsJsonAsync("/api/auth/captcha", new
        {
            challenge_id = challengeId,
            captcha_code = "1234",
        });
        var submitDoc = await ReadJsonAsync(submit);
        var sessionId = GetData(submitDoc).GetProperty("session_id").GetString();

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.True(submitDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.NotNull(sessionId);
        Assert.True(GetData(submitDoc).GetProperty("connected").GetBoolean());
        Assert.Equal("auto", GetData(submitDoc).GetProperty("prelogin_mode").GetString());
        Assert.Equal("20260001", GetData(submitDoc).GetProperty("student").GetProperty("student_id").GetString());
        Assert.Equal("软件一班", GetData(submitDoc).GetProperty("student").GetProperty("class_name").GetString());
        Assert.Equal("软件技术", GetData(submitDoc).GetProperty("student").GetProperty("major_name").GetString());
        Assert.Equal("信息工程学院", GetData(submitDoc).GetProperty("student").GetProperty("academy_name").GetString());
        Assert.Equal("主校区", GetData(submitDoc).GetProperty("student").GetProperty("campus_name").GetString());
        Assert.Equal("live_fetch", GetData(submitDoc).GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal("第8周", GetData(submitDoc).GetProperty("cached_schedule").GetProperty("week_label").GetString());
        Assert.Equal(1, fakeSchedule.CookieCalls);

        Assert.Equal(1, fakeSchedule.CookieCalls);

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

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var sessionResponse = await client.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.True(GetData(sessionDoc).GetProperty("connected").GetBoolean());
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
        var fakeSchedule = new DefaultFakeJwScheduleClient();
        var handler = new ProtocolHandler();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwScheduleClient: fakeSchedule,
            jwLoginProtocolFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)));
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);
        var challengeId = GetData(loginDoc).GetProperty("challenge_id").GetString();
        var submit = await client.PostAsJsonAsync("/api/auth/captcha", new
        {
            challenge_id = challengeId,
            captcha_code = "1234",
        });
        var submitDoc = await ReadJsonAsync(submit);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var trustedToken = GetData(submitDoc).GetProperty("trusted_login_token").GetString();

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
        var connectChallengeId = GetData(connectDoc).GetProperty("challenge_id").GetString();

        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);
        Assert.Equal("captcha_required", GetData(connectDoc).GetProperty("status").GetString());

        var connectSubmit = await client.PostAsJsonAsync("/api/auth/captcha", new
        {
            challenge_id = connectChallengeId,
            captcha_code = "1234",
        });
        var connectSubmitDoc = await ReadJsonAsync(connectSubmit);

        Assert.Equal(HttpStatusCode.OK, connectSubmit.StatusCode);
        Assert.True(GetData(connectSubmitDoc).GetProperty("connected").GetBoolean());
        Assert.Equal("auto", GetData(connectSubmitDoc).GetProperty("prelogin_mode").GetString());

        var badTrustedLogin = await client.PostAsJsonAsync("/api/auth/trusted-login", new
        {
            user = "20260001",
            prelogin_mode = "capture",
            trusted_login_token = "bad-token",

        });

        Assert.Equal(HttpStatusCode.Unauthorized, badTrustedLogin.StatusCode);
    }

    [Fact]
    public async Task DesktopBootstrap_RestoresLatestTrustedWorkspaceWithoutFrontendState()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-desktop-bootstrap");
        var handler = new ProtocolHandler();

        await using (var initialHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "desktop-webview",
            jwScheduleClient: new DefaultFakeJwScheduleClient(),
            jwLoginProtocolFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler))))
        {
            var login = await initialHost.Client.PostAsJsonAsync("/api/auth/login", new
            {
                user = "20260001",
                password = "secret",
                prelogin_mode = "capture",
            });
            var loginDoc = await ReadJsonAsync(login);
            var challengeId = GetData(loginDoc).GetProperty("challenge_id").GetString();
            var submit = await initialHost.Client.PostAsJsonAsync("/api/auth/captcha", new
            {
                challenge_id = challengeId,
                captcha_code = "1234",
            });
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        }

        await using var desktopHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "desktop-webview",
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var bootstrap = await desktopHost.Client.PostAsJsonAsync("/api/desktop/auth-bootstrap", new { });
        var bootstrapDoc = await ReadJsonAsync(bootstrap);
        var bootstrapData = GetData(bootstrapDoc);
        var authData = bootstrapData.GetProperty("auth");

        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        Assert.Equal("20260001", bootstrapData.GetProperty("user").GetString());
        Assert.False(authData.GetProperty("connected").GetBoolean());
        Assert.Equal("local_cache", authData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.False(string.IsNullOrWhiteSpace(authData.GetProperty("session_id").GetString()));
    }

    [Fact]
    public async Task AuthLogin_FirstInitializationRollsBackUnexpectedScheduleExceptions()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-auth-webhost-init-unexpected-fail");
        var fakeSchedule = new DefaultFakeJwScheduleClient
        {
            CookieException = new InvalidOperationException("模拟课表落库异常"),
        };
        var handler = new ProtocolHandler();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-auth",
            jwScheduleClient: fakeSchedule,
            jwLoginProtocolFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)));
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);
        var challengeId = GetData(loginDoc).GetProperty("challenge_id").GetString();
        var submit = await client.PostAsJsonAsync("/api/auth/captcha", new
        {
            challenge_id = challengeId,
            captcha_code = "1234",
        });
        var submitDoc = await ReadJsonAsync(submit);

        Assert.Equal(HttpStatusCode.InternalServerError, submit.StatusCode);

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
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);

        // Incomplete local workspaces rebuild through the school protocol instead of trapping the form.
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("captcha_required", GetData(loginDoc).GetProperty("status").GetString());
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

    private static JsonElement GetData(JsonDocument document)
    {
        return document.RootElement.GetProperty("data");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }
}
