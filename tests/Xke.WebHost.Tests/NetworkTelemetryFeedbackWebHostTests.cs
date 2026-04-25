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
using Xke.Core.Notice;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NetworkTelemetryFeedbackWebHostTests
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
            HeartbeatEnabled = true,
            HeartbeatIntervalMinutes = 5,
            CampusIp = "10.10.0.8",
            CampusSubnetPrefix = 24,
            CampusGateway = "10.10.0.1",
            CampusDns = "223.5.5.5",
            TelecomAccount = "telecom-user",
            TelecomPassword = "telecom-pass",
        });
        var saveDoc = await ReadJsonAsync(save);
        var saveData = GetData(saveDoc);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("校园网配置已保存，但当前进程未以管理员权限运行，未实际修改本机 IP", saveDoc.RootElement.GetProperty("msg").GetString());
        Assert.Equal("campus", saveData.GetProperty("settings").GetProperty("provider").GetString());
        Assert.True(saveData.GetProperty("apply_result").GetProperty("requires_elevation").GetBoolean());

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
        Assert.Equal("校园账号", statusData.GetProperty("provider_label").GetString());

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
        Assert.Contains(executor.Commands, command => command.StartsWith("netsh interface ip set address", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TelemetryAndFeedbackEndpoints_RecordLocalOnlyData()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-telemetry-feedback");

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-telemetry-feedback",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");

        using var telemetryRequest = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry/report");
        telemetryRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        telemetryRequest.Content = JsonContent.Create(new TelemetryReportRequest
        {
            Reason = "manual",
            SessionStartedAt = "2026-04-23T12:00:00Z",
            ForegroundSecondsDelta = 120,
            BackgroundSecondsDelta = 30,
            ActiveTab = "updates",
            ModuleHint = "about",
            Build = "2026.04.23",
            StudentId = "20260001",
            FeatureUsageDeltas = new Dictionary<string, object>
            {
                ["updates"] = new Dictionary<string, object>
                {
                    ["open_count"] = 1,
                    ["foreground_seconds_delta"] = 120,
                    ["background_seconds_delta"] = 30,
                },
            },
        });
        var telemetry = await client.SendAsync(telemetryRequest);
        var telemetryDoc = await ReadJsonAsync(telemetry);
        var telemetryData = GetData(telemetryDoc);

        Assert.Equal(HttpStatusCode.OK, telemetry.StatusCode);
        Assert.True(telemetryData.GetProperty("cloud").GetProperty("queued").GetBoolean());
        Assert.False(telemetryData.GetProperty("cloud").GetProperty("skipped").GetBoolean());
        Assert.Equal("spooled-local-only", telemetryData.GetProperty("cloud").GetProperty("reason").GetString());
        Assert.Contains("students/20260001/info/remote_sensing_snapshot.json", telemetryData.GetProperty("snapshot_path").GetString());

        var overview = await client.GetAsync($"/api/telemetry/overview?session_id={sessionId}");
        var overviewDoc = await ReadJsonAsync(overview);
        var overviewData = GetData(overviewDoc);
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        Assert.Equal(1, overviewData.GetProperty("student_count").GetInt32());
        Assert.Equal(150, overviewData.GetProperty("total_runtime_seconds").GetInt32());

        var profile = await client.GetAsync($"/api/telemetry/profile?session_id={sessionId}&student_id=20260001");
        var profileDoc = await ReadJsonAsync(profile);
        var profileData = GetData(profileDoc);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("20260001", profileData.GetProperty("student_id").GetString());
        Assert.Equal(1, profileData.GetProperty("runtime").GetProperty("session_count").GetInt32());

        var feedback = await client.PostAsJsonAsync($"/api/feedback/report?session_id={sessionId}", new FeedbackRequest
        {
            Category = "bug",
            Content = "network panel needs help",
            Contact = "tester@example.com",
            Build = "2026.04.23",
            CurrentTab = "updates",
        });
        var feedbackDoc = await ReadJsonAsync(feedback);
        var feedbackData = GetData(feedbackDoc);
        Assert.Equal(HttpStatusCode.OK, feedback.StatusCode);
        Assert.Contains(".feedback/outbox/20260001/", feedbackData.GetProperty("stored_key").GetString());
        Assert.True(File.Exists(Path.Combine(dataRoot.RootPath, feedbackData.GetProperty("stored_key").GetString()!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task BlockedStudent_NetworkTelemetryAndFeedbackRoutesReturn403Detail()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-network-blocked");
        var fixture = CreateBlockedNoticeFixture("20269999");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20269999",
            Password = "secret",
            StudentId = "20269999",
            TrustedLoginToken = "trusted-token",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        root.ForStudent("20269999").SaveProfile(new StudentProfileDocument
        {
            StudentId = "20269999",
            Name = "受限学生",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        var session = root.Sessions().Create("20269999", "20269999", "capture");

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-network-blocked",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(),
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;

        using var networkRequest = new HttpRequestMessage(HttpMethod.Get, "/api/network/status");
        networkRequest.Headers.Add(SessionConstants.SessionHeaderName, session.SessionId);
        var network = await client.SendAsync(networkRequest);
        var networkDoc = await ReadJsonAsync(network);
        Assert.Equal(HttpStatusCode.Forbidden, network.StatusCode);
        Assert.True(networkDoc.RootElement.GetProperty("detail").GetProperty("blocked").GetBoolean());

        var telemetry = await client.PostAsJsonAsync($"/api/telemetry/report?session_id={session.SessionId}", new TelemetryReportRequest());
        var telemetryDoc = await ReadJsonAsync(telemetry);
        Assert.Equal(HttpStatusCode.Forbidden, telemetry.StatusCode);
        Assert.Equal("20269999", telemetryDoc.RootElement.GetProperty("detail").GetProperty("student_id").GetString());

        var feedback = await client.PostAsJsonAsync($"/api/feedback/report?session_id={session.SessionId}", new FeedbackRequest
        {
            Content = "blocked",
        });
        var feedbackDoc = await ReadJsonAsync(feedback);
        Assert.Equal(HttpStatusCode.Forbidden, feedback.StatusCode);
        Assert.True(feedbackDoc.RootElement.GetProperty("detail").GetProperty("blocked").GetBoolean());
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

    private static NoticeFixture CreateBlockedNoticeFixture(string blockedStudentId)
    {
        const string updatedAt = "2026-04-23T12:00:00Z";
        var noticeDirectory = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["manifests"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "beta",
                    ["path"] = "notice/beta.json",
                    ["updated_at"] = updatedAt,
                    ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(new Dictionary<string, object?>
                    {
                        ["schema_version"] = "1",
                        ["manifest_id"] = "beta",
                        ["updated_at"] = updatedAt,
                        ["bundles"] = new List<object?>(),
                    })),
                },
            },
        };
        var manifest = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["manifest_id"] = "beta",
            ["updated_at"] = updatedAt,
            ["bundles"] = new List<object?>(),
        };
        var versionPolicy = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["versions"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "beta",
                    ["version"] = "1.0.0",
                    ["enabled"] = true,
                    ["updated_at"] = updatedAt,
                    ["app_control"] = new Dictionary<string, object?>
                    {
                        ["state"] = "normal",
                        ["reason"] = string.Empty,
                    },
                    ["feature_flags"] = new Dictionary<string, object?>(),
                    ["feature_limits"] = new Dictionary<string, object?>(),
                    ["runtime_policy"] = new Dictionary<string, object?>(),
                    ["telemetry_enabled"] = false,
                },
            },
        };
        var updateInfo = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["latest_version"] = "1.0.0",
        };
        var accessPolicy = new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["updated_at"] = updatedAt,
            ["default_message"] = "该账号当前不可用",
            ["blocked_students"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["student_id_hash"] = NoticeHash.Sha256Hex(Encoding.UTF8.GetBytes(blockedStudentId)),
                    ["message"] = "该账号当前不可用",
                },
            },
        };
        var index = new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["updated_at"] = updatedAt,
            ["notice"] = new Dictionary<string, object?>
            {
                ["path"] = "notice/directory.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(noticeDirectory)),
            },
            ["version_policy"] = new Dictionary<string, object?>
            {
                ["path"] = "config/version_policy.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(versionPolicy)),
            },
            ["update_info"] = new Dictionary<string, object?>
            {
                ["path"] = "config/update_info.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(updateInfo)),
            },
            ["access_policy"] = new Dictionary<string, object?>
            {
                ["path"] = "config/access_policy.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(accessPolicy)),
            },
        };

        var cloudFeed = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["announcement"] = new Dictionary<string, object?>
            {
                ["items"] = new List<object?>(),
            },
            ["policy"] = new Dictionary<string, object?>
            {
                ["version_policy"] = versionPolicy,
                ["access_policy"] = accessPolicy,
            },
            ["update_info"] = updateInfo,
        };

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cloud-feed.json"] = SerializeJson(cloudFeed),
            ["index.json"] = SerializeJson(index),
            ["index.sig"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(ecdsa.SignData(NoticeCanonicalJson.Serialize(index), HashAlgorithmName.SHA512)).TrimEnd('=')),
            ["notice/directory.json"] = SerializeJson(noticeDirectory),
            ["notice/beta.json"] = SerializeJson(manifest),
            ["config/version_policy.json"] = SerializeJson(versionPolicy),
            ["config/update_info.json"] = SerializeJson(updateInfo),
            ["config/access_policy.json"] = SerializeJson(accessPolicy),
        };

        return new NoticeFixture(new MemoryNoticeRemoteSource(files), ecdsa.ExportSubjectPublicKeyInfoPem());
    }

    private static byte[] SerializeJson(object payload)
    {
        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    private sealed record NoticeFixture(MemoryNoticeRemoteSource Source, string PublicKeyPem);

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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("http://127.0.0.1/");
            if (uri.Host.Equals("10.199.199.199", StringComparison.OrdinalIgnoreCase))
            {
                if (uri.AbsolutePath.Contains("/drcom/login", StringComparison.OrdinalIgnoreCase))
                {
                    LoginRequested = true;
                    return Task.FromResult(CreateJsonpResponse(uri, "dr1001({\"result\":1,\"msg\":\"ok\"})"));
                }

                return Task.FromResult(CreateHtmlResponse(uri,
                    "<html><script>var lip='10.10.0.8';var v4ip='10.10.0.9';var uid='20260001';var NID='测试同学';var stime='2026-04-23 12:00:00';var etime='';var time='3600';var flow='2048';</script></html>"));
            }

            if (uri.Host.Contains("baidu.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("qq.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(LoginRequested
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
