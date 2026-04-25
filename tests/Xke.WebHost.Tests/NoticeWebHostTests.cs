using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Core.Desktop;
using Xke.Security;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NoticeWebHostTests
{
    [Fact]
    public async Task ConfigAndUpdatesFeed_LoadFromSignedRemoteSource()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-notice-webhost-data");
        var fixture = CreateNoticeFixture();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-notice",
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;

        var config = await client.GetFromJsonAsync<JsonElement>("/api/config?version=1.2.3&channel=beta");
        Assert.Equal("cloud", config.GetProperty("remote").GetProperty("source").GetString());
        Assert.Equal("1.2.3", config.GetProperty("remote").GetProperty("config").GetProperty("latest_version").GetString());
        Assert.True(config.GetProperty("remote").GetProperty("config").GetProperty("feature_flags").GetProperty("schedule_view").GetBoolean());

        var anonymousFeedResponse = await client.GetAsync("/api/updates/feed?student_id=20260001&version=1.2.3&channel=beta&trigger=manual");
        var anonymousFeed = await ReadJsonAsync(anonymousFeedResponse);
        Assert.Equal(HttpStatusCode.OK, anonymousFeedResponse.StatusCode);
        Assert.Equal("20260001", GetData(anonymousFeed).GetProperty("student_id").GetString());
        Assert.Equal("cloud", GetData(anonymousFeed).GetProperty("feed").GetProperty("source").GetString());
        Assert.Equal(2, GetData(anonymousFeed).GetProperty("feed").GetProperty("items").GetArrayLength());
        Assert.Equal(1, GetData(anonymousFeed).GetProperty("feed").GetProperty("reminder_items").GetArrayLength());

        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001",
            Password = "secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-token",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        root.ForStudent("20260001").SaveProfile(new StudentProfileDocument
        {
            StudentId = "20260001",
            Name = "测试同学",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        var session = root.Sessions().Create("20260001", "20260001", "capture");

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/updates/feed?version=1.2.3&channel=beta");
        sessionRequest.Headers.Add("X-XKE-Session", session.SessionId);
        var sessionFeedResponse = await client.SendAsync(sessionRequest);
        var sessionFeed = await ReadJsonAsync(sessionFeedResponse);
        Assert.Equal(HttpStatusCode.OK, sessionFeedResponse.StatusCode);
        Assert.Equal("20260001", GetData(sessionFeed).GetProperty("student_id").GetString());
        Assert.False(GetData(sessionFeed).GetProperty("feed").GetProperty("access_decision").GetProperty("blocked").GetBoolean());
    }

    [Fact]
    public async Task BlockedStudent_SessionEndpointReturns403Detail()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-notice-blocked-auth");
        var fixture = CreateNoticeFixture(blockedStudentId: "20269999");
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
            hostMode: "test-notice",
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("X-XKE-Session", session.SessionId);
        var response = await client.SendAsync(request);
        var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("detail").GetProperty("blocked").GetBoolean());
        Assert.Equal("20269999", document.RootElement.GetProperty("detail").GetProperty("student_id").GetString());
        Assert.Equal("access-policy-blocked", document.RootElement.GetProperty("detail").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ConfigLoadsRemoteFeedWithoutTrustingIndexSignature()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-notice-invalid-signature");
        var fixture = CreateNoticeFixture(invalidSignature: true);

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-notice",
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;

        var config = await client.GetFromJsonAsync<JsonElement>("/api/config?version=1.2.3&channel=beta");
        var remote = config.GetProperty("remote");

        Assert.Equal("cloud", remote.GetProperty("source").GetString());
        Assert.Equal("1.2.3", remote.GetProperty("config").GetProperty("latest_version").GetString());
        Assert.Equal("normal", remote.GetProperty("config").GetProperty("app_control").GetProperty("state").GetString());
    }

    [Fact]
    public async Task ConfigLoadsRemoteFeedWithoutBlockingOnPayloadHash()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-notice-invalid-hash");
        var fixture = CreateNoticeFixture(invalidUpdateInfoHash: true);

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-notice",
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;

        var config = await client.GetFromJsonAsync<JsonElement>("/api/config?version=1.2.3&channel=beta");
        var remote = config.GetProperty("remote");

        Assert.Equal("cloud", remote.GetProperty("source").GetString());
        Assert.Equal("1.2.3", remote.GetProperty("config").GetProperty("latest_version").GetString());
        Assert.Equal("normal", remote.GetProperty("config").GetProperty("app_control").GetProperty("state").GetString());
    }

    [Fact]
    public async Task UpdatesFeedReturnsTrustedLocalCacheWhenRemoteUnavailableAfterInitialSync()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-notice-last-good-cache");
        var fixture = CreateNoticeFixture();

        await using (var firstHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-notice",
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem))
        {
            var initialResponse = await firstHost.Client.GetAsync("/api/updates/feed?student_id=20260001&version=1.2.3&channel=beta&trigger=manual");
            Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        }

        await using var secondHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-notice",
            noticeRemoteSource: new MissingNoticeRemoteSource(),
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);

        var cachedResponse = await secondHost.Client.GetAsync("/api/updates/feed?student_id=20260001&version=1.2.3&channel=beta&trigger=manual");
        var cachedFeed = await ReadJsonAsync(cachedResponse);
        var feed = GetData(cachedFeed).GetProperty("feed");

        Assert.Equal(HttpStatusCode.OK, cachedResponse.StatusCode);
        Assert.Equal("local-cache", feed.GetProperty("source").GetString());
        Assert.Equal(2, feed.GetProperty("items").GetArrayLength());
        Assert.Contains(feed.GetProperty("warnings").EnumerateArray(), item => item.GetString()?.Contains("云端入口读取失败", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task UpdatesFeedWritesNoticeCacheInsideRuntimeDataRoot()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-notice-cache-location");
        var fixture = CreateNoticeFixture();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-notice",
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);

        var response = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&version=1.2.3&channel=beta&trigger=manual");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dataRootModel = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);

        Assert.StartsWith(dataRoot.RootPath, dataRootModel.CloudDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(dataRootModel.CloudFeedPath));
        Assert.True(File.Exists(dataRootModel.CloudFeedMetaPath));
        Assert.Contains($"{Path.DirectorySeparatorChar}.cloud", dataRootModel.CloudDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(dataRoot.RootPath, "notice")));
    }

    [Fact]
    public async Task DesktopSettings_RoundTripAndPersistAcrossRestarts()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-desktop-settings");
        var startupRegistrar = new FakeDesktopStartupRegistrar();

        async Task<XkeWebHostService> CreateServiceAsync()
        {
            var service = new XkeWebHostService(new XkeWebHostOptions
            {
                HostMode = "test-desktop",
                RuntimeChannel = "dotnet8-test",
                WebRootPath = webRoot.RootPath,
                AuthDataRootPath = dataRoot.RootPath,
                NoticeRemoteSource = new MissingNoticeRemoteSource(),
                DesktopStartupRegistrar = startupRegistrar,
                Urls = new[] { "http://127.0.0.1:0" },
            });
            await service.StartAsync();
            return service;
        }

        await using var firstService = await CreateServiceAsync();
        using (var firstClient = new HttpClient { BaseAddress = new Uri(firstService.LaunchResult!.BaseUrl) })
        {
            var initial = await firstClient.GetFromJsonAsync<JsonElement>("/api/desktop/settings");
            Assert.False(initial.GetProperty("data").GetProperty("settings").GetProperty("launch_at_startup").GetBoolean());
            Assert.True(initial.GetProperty("data").GetProperty("settings").GetProperty("close_to_tray").GetBoolean());
            Assert.False(initial.GetProperty("data").GetProperty("startup_registered").GetBoolean());

            var startupResponse = await firstClient.PostAsJsonAsync("/api/desktop/settings", new
            {
                launch_at_startup = true,
                silent_startup = true,
            });
            var startupSaved = await ReadJsonAsync(startupResponse);
            Assert.Equal(HttpStatusCode.OK, startupResponse.StatusCode);
            Assert.True(GetData(startupSaved).GetProperty("settings").GetProperty("launch_at_startup").GetBoolean());
            Assert.True(GetData(startupSaved).GetProperty("settings").GetProperty("silent_startup").GetBoolean());
            Assert.True(GetData(startupSaved).GetProperty("startup_registered").GetBoolean());
            Assert.Equal(1, startupRegistrar.RegisterCalls);

            var saveResponse = await firstClient.PostAsJsonAsync("/api/desktop/settings", new
            {
                launch_at_startup = false,
                silent_startup = true,
                close_to_tray = false,
            });
            var saved = await ReadJsonAsync(saveResponse);
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            Assert.False(GetData(saved).GetProperty("settings").GetProperty("launch_at_startup").GetBoolean());
            Assert.False(GetData(saved).GetProperty("settings").GetProperty("silent_startup").GetBoolean());
            Assert.False(GetData(saved).GetProperty("settings").GetProperty("close_to_tray").GetBoolean());
            Assert.False(GetData(saved).GetProperty("startup_registered").GetBoolean());
            Assert.Equal(1, startupRegistrar.UnregisterCalls);
        }

        await firstService.StopAsync();

        await using var secondService = await CreateServiceAsync();
        using var secondClient = new HttpClient { BaseAddress = new Uri(secondService.LaunchResult!.BaseUrl) };
        var restored = await secondClient.GetFromJsonAsync<JsonElement>("/api/desktop/settings");

        Assert.False(restored.GetProperty("data").GetProperty("settings").GetProperty("launch_at_startup").GetBoolean());
        Assert.False(restored.GetProperty("data").GetProperty("settings").GetProperty("silent_startup").GetBoolean());
        Assert.False(restored.GetProperty("data").GetProperty("settings").GetProperty("close_to_tray").GetBoolean());
        Assert.False(restored.GetProperty("data").GetProperty("startup_registered").GetBoolean());
    }

    [Fact]
    public async Task DesktopSettings_StartupRegistrationFailureDoesNotPersistFalseSuccess()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-desktop-settings-failure");
        var startupRegistrar = new FakeDesktopStartupRegistrar
        {
            ThrowOnRegister = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-desktop",
            desktopStartupRegistrar: startupRegistrar);
        var client = host.Client;

        var saveResponse = await client.PostAsJsonAsync("/api/desktop/settings", new
        {
            launch_at_startup = true,
            silent_startup = true,
        });
        var saved = await ReadJsonAsync(saveResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, saveResponse.StatusCode);
        Assert.Contains("模拟注册失败", saved.RootElement.GetProperty("detail").GetString());

        var restored = await client.GetFromJsonAsync<JsonElement>("/api/desktop/settings");
        Assert.False(restored.GetProperty("data").GetProperty("settings").GetProperty("launch_at_startup").GetBoolean());
        Assert.False(restored.GetProperty("data").GetProperty("settings").GetProperty("silent_startup").GetBoolean());
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

    private sealed class FakeDesktopStartupRegistrar : IDesktopStartupRegistrar
    {
        public bool Registered { get; private set; }
        public bool ThrowOnRegister { get; init; }
        public int RegisterCalls { get; private set; }
        public int UnregisterCalls { get; private set; }

        public bool? GetRegistrationState()
        {
            return Registered;
        }

        public void SetRegistrationState(bool enabled)
        {
            if (enabled)
            {
                RegisterCalls++;
                if (ThrowOnRegister)
                {
                    throw new InvalidOperationException("模拟注册失败");
                }
            }
            else
            {
                UnregisterCalls++;
            }

            Registered = enabled;
        }
    }

    private static NoticeFixture CreateNoticeFixture(string blockedStudentId = "", bool invalidSignature = false, bool invalidUpdateInfoHash = false)
    {
        const string updatedAt = "2026-04-23T12:00:00Z";
        const string bundlePath = "notice/bundles/operations.json";
        const string contentPath = "notice/content/welcome.md";
        const string contentText = "# 欢迎使用 XKE\n\n这是一个脱敏公告样本。";

        var bundle = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["bundle_id"] = "operations",
            ["updated_at"] = updatedAt,
            ["title"] = "运行公告",
            ["summary"] = "控制面公告集合",
            ["items"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "welcome",
                    ["title"] = "欢迎使用",
                    ["summary"] = "主面板公告",
                    ["display_mode"] = "panel",
                    ["kind"] = "announcement",
                    ["severity"] = "info",
                    ["pinned"] = true,
                    ["updated_at"] = updatedAt,
                    ["content_path"] = "../content/welcome.md",
                    ["content_sha256"] = NoticeHash.Sha256Hex(NoticeCompatibility.NoticeTextBytes(contentText)),
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "maintenance",
                    ["title"] = "维护提醒",
                    ["summary"] = "这是一个提醒项",
                    ["display_mode"] = "reminder",
                    ["kind"] = "maintenance",
                    ["severity"] = "important",
                    ["updated_at"] = "2026-04-23T13:00:00Z",
                    ["content"] = "今晚 23:00 进行维护。",
                },
            },
        };

        var manifest = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["manifest_id"] = "beta",
            ["updated_at"] = updatedAt,
            ["title"] = "XKE 控制面",
            ["subtitle"] = "脱敏公告",
            ["notice_text"] = "当前为测试控制面配置",
            ["bundles"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["bundle_id"] = "operations",
                    ["path"] = bundlePath,
                    ["updated_at"] = updatedAt,
                    ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(bundle)),
                },
            },
        };

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
                    ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(manifest)),
                },
            },
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
                    ["version"] = "1.2.3",
                    ["enabled"] = true,
                    ["updated_at"] = updatedAt,
                    ["lifecycle"] = new Dictionary<string, object?>(),
                    ["app_control"] = new Dictionary<string, object?>
                    {
                        ["state"] = "normal",
                        ["reason"] = string.Empty,
                    },
                    ["feature_flags"] = new Dictionary<string, object?>
                    {
                        ["schedule_view"] = true,
                        ["public_course_preview"] = true,
                        ["course_grab"] = false,
                        ["campus_login"] = true,
                        ["keepalive"] = true,
                    },
                    ["feature_limits"] = new Dictionary<string, object?>
                    {
                        ["course_grab"] = new Dictionary<string, object?>
                        {
                            ["min_interval_sec"] = 0.5d,
                            ["max_runtime_sec"] = 5,
                            ["cooldown_sec"] = 86400,
                        },
                    },
                    ["runtime_policy"] = new Dictionary<string, object?>
                    {
                        ["config_pull_interval_sec"] = 300,
                        ["login_sync_cooldown_sec"] = 300,
                        ["summary_report_interval_sec"] = 600,
                    },
                    ["telemetry_enabled"] = false,
                },
            },
        };

        var updateInfo = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["latest_version"] = "1.2.3",
            ["min_supported_version"] = "1.0.0",
            ["download_url"] = "https://example.invalid/download",
        };

        var accessPolicy = new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["updated_at"] = updatedAt,
            ["default_message"] = "该账号当前不可用",
            ["blocked_students"] = string.IsNullOrWhiteSpace(blockedStudentId)
                ? new List<object?>()
                : new List<object?>
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
                ["sha256"] = invalidUpdateInfoHash
                    ? new string('0', 64)
                    : NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(updateInfo)),
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
                ["title"] = "XKE 控制面",
                ["subtitle"] = "脱敏公告",
                ["notice_text"] = "当前为测试控制面配置",
                ["items"] = bundle["items"],
            },
            ["policy"] = new Dictionary<string, object?>
            {
                ["version_policy"] = versionPolicy,
                ["access_policy"] = accessPolicy,
                ["telemetry_enabled"] = false,
            },
            ["update_info"] = updateInfo,
        };

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var signature = invalidSignature
            ? "invalid-signature"
            : Convert.ToBase64String(ecdsa.SignData(NoticeCanonicalJson.Serialize(index), HashAlgorithmName.SHA512)).TrimEnd('=');

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cloud-feed.json"] = SerializeJson(cloudFeed),
            ["index.json"] = SerializeJson(index),
            ["index.sig"] = Encoding.UTF8.GetBytes(signature),
            ["notice/directory.json"] = SerializeJson(noticeDirectory),
            ["notice/beta.json"] = SerializeJson(manifest),
            [bundlePath] = SerializeJson(bundle),
            [contentPath] = Encoding.UTF8.GetBytes(contentText),
            ["config/version_policy.json"] = SerializeJson(versionPolicy),
            ["config/update_info.json"] = SerializeJson(updateInfo),
            ["config/access_policy.json"] = SerializeJson(accessPolicy),
        };

        return new NoticeFixture(new MemoryNoticeRemoteSource(files), publicKeyPem);
    }

    private static byte[] SerializeJson(object payload)
    {
        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    private sealed record NoticeFixture(MemoryNoticeRemoteSource Source, string PublicKeyPem);

}
