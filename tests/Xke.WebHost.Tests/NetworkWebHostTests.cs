using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Network;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NetworkWebHostTests
{
    [Fact]
    public async Task NetworkEndpoints_SaveConfigDetectStatusAndMaintainHeartbeat()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-network-webhost");
        var handler = new FakeNetworkHttpMessageHandler();
        var executor = new FakeNetworkCommandExecutor();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-network",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(),
            networkHttpMessageHandler: handler,
            networkCommandExecutor: executor);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");

        var save = await client.PostAsJsonAsync($"/api/network/config?session_id={sessionId}", new NetworkConfigRequest
        {
            Provider = "campus",
            CampusAccount = "campus-fixture", CampusPassword = "campus-secret",
            HeartbeatEnabled = true,
            HeartbeatIntervalMinutes = 5,
            TelecomAccount = "telecom-user",
            TelecomPassword = "telecom-pass",
        });
        var saveDoc = await ReadJsonAsync(save);
        var saveData = GetData(saveDoc);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("校园网配置已保存", saveDoc.RootElement.GetProperty("msg").GetString());
        Assert.Equal("campus", saveData.GetProperty("settings").GetProperty("provider").GetString());
        Assert.False(saveData.TryGetProperty("apply_result", out _));

        using var configRequest = new HttpRequestMessage(HttpMethod.Get, "/api/network/config");
        configRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var config = await client.SendAsync(configRequest);
        var configDoc = await ReadJsonAsync(config);
        var configData = GetData(configDoc);
        Assert.Equal(HttpStatusCode.OK, config.StatusCode);
        Assert.Equal("te********ss", configData.GetProperty("credentials").GetProperty("telecom").GetProperty("password").GetString());

        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/api/network/status");
        statusRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var status = await client.SendAsync(statusRequest);
        var statusDoc = await ReadJsonAsync(status);
        var statusData = GetData(statusDoc);

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.True(statusData.GetProperty("campus_connected").GetBoolean());
        Assert.False(statusData.GetProperty("internet_connected").GetBoolean());
        Assert.True(statusData.GetProperty("needs_heartbeat").GetBoolean());
        Assert.Equal("10.199.199.199", statusData.GetProperty("portal_host").GetString());
        Assert.Equal("校园网", statusData.GetProperty("provider_label").GetString());

        var maintain = await client.PostAsJsonAsync($"/api/network/maintain?session_id={sessionId}", new NetworkActionRequest
        {
            Force = true,
        });
        var maintainDoc = await ReadJsonAsync(maintain);
        var maintainData = GetData(maintainDoc);

        Assert.Equal(HttpStatusCode.OK, maintain.StatusCode);
        Assert.Equal("heartbeat", maintainData.GetProperty("action").GetString());
        Assert.True(maintainData.GetProperty("internet_connected").GetBoolean());
        Assert.Equal("校园网心跳已补发，外网访问已恢复", maintainData.GetProperty("message").GetString());
        Assert.Equal(1, maintainData.GetProperty("attempts").GetArrayLength());
        Assert.True(handler.LoginRequested);
        Assert.DoesNotContain(executor.Commands, command => command.StartsWith("netsh", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("10.10.0.8", statusData.GetProperty("interface").GetProperty("ipv4").GetString());
    }

    [Fact]
    public async Task NetworkConfig_DoesNotExposeOrApplyRetiredStaticIpFields()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var executor = new FakeNetworkCommandExecutor();
        await using var host = await WebHostTestServer.StartAsync(web, data,
            jwAuthClient: new FakeJwAuthClient(), jwScheduleClient: new DefaultFakeJwScheduleClient(),
            networkHttpMessageHandler: new FakeNetworkHttpMessageHandler(), networkCommandExecutor: executor);
        var sid = await LoginAsync(host.Client, "20260001");
        using var response = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new
        {
            provider = "campus", campus_ip = "192.0.2.8", campus_subnet_prefix = 24,
            campus_gateway = "192.0.2.1", campus_dns = "192.0.2.53",
        });
        response.EnsureSuccessStatusCode();
        using var payload = await ReadJsonAsync(response);
        var config = GetData(payload);
        foreach (var field in new[] { "campus_ip", "campus_subnet_prefix", "campus_gateway", "campus_dns" })
            Assert.False(config.GetProperty("settings").TryGetProperty(field, out _));
        Assert.False(config.TryGetProperty("apply_result", out _));
        Assert.DoesNotContain(executor.Commands, command => command.StartsWith("netsh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NetworkEndpoints_UseConfiguredPortalAndConnectivityAddress()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var handler = new ConfigurableNetworkHandler();
        var executor = new FakeNetworkCommandExecutor();
        await using var host = await WebHostTestServer.StartAsync(web, data,
            jwAuthClient: new FakeJwAuthClient(), jwScheduleClient: new DefaultFakeJwScheduleClient(),
            networkHttpMessageHandler: handler, networkCommandExecutor: executor);
        var sid = await LoginAsync(host.Client, "20260001");
        using var save = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new NetworkConfigRequest
        {
            PortalUrl = "http://10.20.0.1/", ConnectivityCheckUrls = ["https://example.test/check"],
            HeartbeatEnabled = true, HeartbeatIntervalMinutes = 9,
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var response = await host.Client.GetAsync("/api/network/status?session_id=" + sid);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("10.20.0.1", payload.RootElement.GetProperty("data").GetProperty("portal_host").GetString());
        Assert.Contains("http://10.20.0.1/", handler.Urls);
        Assert.Contains("https://example.test/check", handler.Urls);
        Assert.Contains(executor.Commands, command => command.Contains("10.20.0.1", StringComparison.Ordinal));
    }

    private sealed class ConfigurableNetworkHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent("test-connected") });
        }
    }

    [Theory]
    [InlineData("campus")]
    [InlineData("telecom")]
    [InlineData("unicom")]
    [InlineData("mobile")]
    public async Task NetworkCredentials_AreSeparateForAllProvidersAndPreserveUnchangedPasswords(string provider)
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var handler = new FakeNetworkHttpMessageHandler();
        await using var host = await WebHostTestServer.StartAsync(web, data, jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(), networkHttpMessageHandler: handler, networkCommandExecutor: new FakeNetworkCommandExecutor());
        var sid = await LoginAsync(host.Client, "20260001");
        var root = new XkeDataRoot(data.RootPath);
        var teachingPassword = root.Credentials().Load("20260001")!.Password;
        var patch = new Dictionary<string, object> { ["provider"] = provider, [provider + "_account"] = "separate-network-user", [provider + "_password"] = " network*p@ss " };
        using var save = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, patch);
        save.EnsureSuccessStatusCode();
        using var saved = await ReadJsonAsync(save);
        var bundle = GetData(saved).GetProperty("credentials");
        Assert.Equal(4, bundle.EnumerateObject().Count());
        Assert.Equal("separate-network-user", bundle.GetProperty(provider).GetProperty("account").GetString());
        Assert.DoesNotContain(" network*p@ss ", saved.RootElement.GetRawText());
        using var interval = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new { heartbeat_interval_minutes = 11 });
        interval.EnsureSuccessStatusCode();
        var credential = root.ForStudent("20260001").NetworkCredentials().Load(provider)!;
        Assert.Equal(" network*p@ss ", credential.Password);
        Assert.Equal(teachingPassword, root.Credentials().Load("20260001")!.Password);
        using var maintain = await host.Client.PostAsJsonAsync("/api/network/maintain?session_id=" + sid, new NetworkActionRequest { Force = true });
        maintain.EnsureSuccessStatusCode();
        Assert.StartsWith("separate-network-user", Assert.Single(handler.LoginAccounts));
        Assert.Equal(11, root.ForStudent("20260001").LoadPreferences()!.Network.HeartbeatIntervalMinutes);
    }

    [Theory]
    [InlineData("IP地址不在允许范围", "ip_restricted", false)]
    [InlineData("IP地址不在允许范围", "ip_restricted", true)]
    [InlineData("账号或密码错误", "auth_rejected", false)]
    public async Task NetworkAuthentication_ReportsThePortalFailureWithoutTreatingItAsSuccess(string message, string kind, bool alreadyOnline)
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var handler = new FakeNetworkHttpMessageHandler { LoginBody = "dr1001(" + JsonSerializer.Serialize(new { result = 0, msg = message }) + ");", InternetInitially = alreadyOnline };
        await using var host = await WebHostTestServer.StartAsync(web, data, jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(), networkHttpMessageHandler: handler, networkCommandExecutor: new FakeNetworkCommandExecutor());
        var sid = await LoginAsync(host.Client, "20260001");
        using var save = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new NetworkConfigRequest { CampusAccount = "isolated-user", CampusPassword = "isolated-secret" });
        save.EnsureSuccessStatusCode();
        using var response = await host.Client.PostAsJsonAsync("/api/network/maintain?session_id=" + sid, new NetworkActionRequest { Force = true });
        using var payload = await ReadJsonAsync(response);
        var status = GetData(payload);
        Assert.Contains(message, status.GetProperty("operation_error").GetString());
        Assert.Equal(alreadyOnline, status.GetProperty("internet_connected").GetBoolean());
        var attempt = Assert.Single(status.GetProperty("attempts").EnumerateArray());
        Assert.Equal(kind, attempt.GetProperty("error_kind").GetString());
        Assert.False(attempt.GetProperty("accepted").GetBoolean());
        Assert.False(attempt.TryGetProperty("preview", out _));
        Assert.DoesNotContain("isolated-secret", payload.RootElement.GetRawText());
        Assert.DoesNotContain("isolated-user", payload.RootElement.GetRawText());
    }

    [Fact]
    public async Task CampusAuthentication_DoesNotImplicitlyForwardTeachingCredentials()
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var handler = new FakeNetworkHttpMessageHandler();
        await using var host = await WebHostTestServer.StartAsync(web, data, jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(), networkHttpMessageHandler: handler, networkCommandExecutor: new FakeNetworkCommandExecutor());
        var sid = await LoginAsync(host.Client, "20260001");
        using var response = await host.Client.PostAsJsonAsync("/api/network/maintain?session_id=" + sid, new NetworkActionRequest { Force = true });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(handler.LoginAccounts);
        using var payload = await ReadJsonAsync(response);
        Assert.Contains("未配置校园网账号或密码", payload.RootElement.GetProperty("msg").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public async Task NetworkConfig_RejectsInvalidHeartbeatIntervalsWithoutSaving(int minutes)
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        await using var host = await WebHostTestServer.StartAsync(web, data, jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(), networkHttpMessageHandler: new FakeNetworkHttpMessageHandler(), networkCommandExecutor: new FakeNetworkCommandExecutor());
        var sid = await LoginAsync(host.Client, "20260001");
        using var response = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new { heartbeat_interval_minutes = minutes });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(5, new XkeDataRoot(data.RootPath).ForStudent("20260001").LoadPreferences()!.Network.HeartbeatIntervalMinutes);
    }

    [Theory]
    [InlineData("<script>secret-untrusted-response</script>", "response_error", 200)]
    [InlineData("dr1001({\"result\":0,\"ret_code\":35})", "auth_rejected", 200)]
    [InlineData("dr1001({\"result\":0,\"msg\":\"isolated-user isolated-secret IP not allowed\"})", "ip_restricted", 403)]
    [InlineData("dr1001({\"result\":1,\"msg\":\"ok\"})", "response_error", 503)]
    [InlineData("dr1001({\"result\":0,\"msg\":\"服务繁忙\"})", "server_error", 503)]
    public async Task NetworkAuthentication_DoesNotExposeUntrustedRawResponsesOrEchoedCredentials(string body, string kind, int statusCode)
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var handler = new FakeNetworkHttpMessageHandler { LoginBody = body, LoginStatus = (HttpStatusCode)statusCode };
        await using var host = await WebHostTestServer.StartAsync(web, data, jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(), networkHttpMessageHandler: handler, networkCommandExecutor: new FakeNetworkCommandExecutor());
        var sid = await LoginAsync(host.Client, "20260001");
        using var save = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new NetworkConfigRequest { CampusAccount = "isolated-user", CampusPassword = "isolated-secret" });
        save.EnsureSuccessStatusCode();
        using var response = await host.Client.PostAsJsonAsync("/api/network/maintain?session_id=" + sid, new NetworkActionRequest { Force = true });
        using var payload = await ReadJsonAsync(response);
        var status = GetData(payload);
        Assert.Equal(kind, status.GetProperty("attempts")[0].GetProperty("error_kind").GetString());
        foreach (var secret in new[] { "isolated-user", "isolated-secret", "secret-untrusted-response" }) Assert.DoesNotContain(secret, payload.RootElement.GetRawText());
    }

    [Theory]
    [InlineData("telecom")]
    [InlineData("unicom")]
    [InlineData("mobile")]
    public async Task NetworkAuthentication_IpRestrictionDoesNotTriggerAnotherProviderSuffixAttempt(string provider)
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var handler = new FakeNetworkHttpMessageHandler { LoginBody = "dr1001(" + JsonSerializer.Serialize(new { result = 0, msg = "当前IP不在允许范围" }) + ")" };
        await using var host = await WebHostTestServer.StartAsync(web, data, jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(), networkHttpMessageHandler: handler, networkCommandExecutor: new FakeNetworkCommandExecutor());
        var sid = await LoginAsync(host.Client, "20260001");
        using var save = await host.Client.PostAsJsonAsync("/api/network/config?session_id=" + sid, new Dictionary<string, object>
        { ["provider"] = provider, [provider + "_account"] = "operator-user", [provider + "_password"] = "operator-secret" });
        save.EnsureSuccessStatusCode();
        using var response = await host.Client.PostAsJsonAsync("/api/network/maintain?session_id=" + sid, new NetworkActionRequest { Force = true });
        response.EnsureSuccessStatusCode();
        Assert.Single(handler.LoginAccounts);
    }

    private static async Task<string> LoginAsync(HttpClient client, string user)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user,
            password = "secret",
            prelogin_mode = "capture",
        });
        var document = await ReadJsonAsync(login);
        return GetData(document).GetProperty("session_id").GetString()!;
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
        public Task<JwAuthResult> InitializeAsync(string user, string password, string preloginMode, CancellationToken cancellationToken = default, MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            return Task.FromResult(new JwAuthResult(BuildProfile(user), "mobile-token-initial"));
        }

        public Task<JwAuthResult> ConnectAsync(string user, string password, string preloginMode, CancellationToken cancellationToken = default, MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            return Task.FromResult(new JwAuthResult(BuildProfile(user), "mobile-token-connected"));
        }

        private static StudentProfileDocument BuildProfile(string user)
        {
            return new StudentProfileDocument
            {
                StudentId = user,
                Name = "测试同学",
                ClassName = "软件一班",
                MajorName = "软件技术",
                AcademyName = "信息工程学院",
                TrainingLevel = "专科",
                CampusName = "主校区",
                UpdatedAt = "2026-04-23T12:00:00Z",
            };
        }
    }

    private sealed class FakeJwScheduleClient : Xke.Core.Schedule.IJwScheduleClient
    {
        public Task<Xke.Core.Schedule.JwScheduleFetchResult> FetchPreferredAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            throw new NotSupportedException();
        }

        public Task<Xke.Core.Schedule.JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null, string sessionId = "")
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeNetworkCommandExecutor : INetworkCommandExecutor
    {
        private const string IpConfigSample = """

Windows IP 配置

以太网适配器 Ethernet:
   连接特定的 DNS 后缀 . . . . . . . :
   描述. . . . . . . . . . . . . . . . : Realtek PCIe GbE Family Controller
   物理地址. . . . . . . . . . . . . . : 00-11-22-33-44-55
   DHCP 已启用 . . . . . . . . . . . . : 否
   IPv4 地址 . . . . . . . . . . . . . : 10.10.0.8(首选)
   子网掩码  . . . . . . . . . . . . . : 255.255.255.0
   默认网关. . . . . . . . . . . . . . : 10.10.0.1
   DNS 服务器  . . . . . . . . . . . . : 223.5.5.5
""";

        public List<string> Commands { get; } = [];

        public Task<NetworkCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var command = $"{fileName} {string.Join(' ', arguments)}".Trim();
            Commands.Add(command);

            if (string.Equals(fileName, "ping", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new NetworkCommandResult(0, "ping ok", string.Empty));
            }

            if (string.Equals(fileName, "ipconfig", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new NetworkCommandResult(0, IpConfigSample, string.Empty));
            }

            if (string.Equals(fileName, "netsh", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new NetworkCommandResult(1, string.Empty, "需要管理员权限"));
            }

            return Task.FromResult(new NetworkCommandResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class FakeNetworkHttpMessageHandler : HttpMessageHandler
    {
        public bool LoginRequested { get; private set; }
        public string LoginBody { get; set; } = "dr1001({\"result\":1,\"msg\":\"ok\"})";
        public bool InternetInitially { get; set; }
        public HttpStatusCode LoginStatus { get; set; } = HttpStatusCode.OK;
        public List<string> LoginAccounts { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("http://127.0.0.1/");
            if (uri.Host.Equals("10.199.199.199", StringComparison.OrdinalIgnoreCase))
            {
                if (uri.AbsolutePath.Contains("/drcom/login", StringComparison.OrdinalIgnoreCase))
                {
                    LoginRequested = true;
                    var fields = uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=', 2)).ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
                    LoginAccounts.Add(fields["DDDDD"]);
                    var response = CreateJsonpResponse(uri, LoginBody);
                    response.StatusCode = LoginStatus;
                    return Task.FromResult(response);
                }

                return Task.FromResult(CreateHtmlResponse(uri,
                    "<html><script>var lip='10.10.0.8';var v4ip='10.10.0.9';var uid='20260001';var NID='测试同学';var stime='2026-04-23 12:00:00';var etime='';var time='3600';var flow='2048';</script></html>"));
            }

            if (uri.Host.Contains("baidu.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("qq.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(LoginRequested || InternetInitially
                    ? CreateHtmlResponse(uri, "<html><body>ok</body></html>")
                    : CreateHtmlResponse(uri, "<html><body>Dr.COM 注销页</body></html>"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request,
                Content = new StringContent(string.Empty),
            });
        }

        private static HttpResponseMessage CreateHtmlResponse(Uri uri, string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, uri),
                Content = new StringContent(body, Encoding.UTF8, "text/html"),
            };
        }

        private static HttpResponseMessage CreateJsonpResponse(Uri uri, string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, uri),
                Content = new StringContent(body, Encoding.UTF8, "application/javascript"),
            };
        }
    }

}
