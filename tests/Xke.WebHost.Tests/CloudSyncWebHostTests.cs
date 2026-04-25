using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Core.CloudSync;
using Xke.Core.Notice;
using Xke.Core.Telemetry;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class CloudSyncWebHostTests
{
    [Fact]
    public void HttpNoticeRemoteSource_DefaultBaseUrlIsDesensitizedPlaceholder()
    {
        Assert.Equal(
            "https://example.invalid/xke-cloud/",
            HttpNoticeRemoteSource.DefaultBaseUrl);
    }

    [Fact]
    public void CloudEndpointResolvers_LoadIgnoredPrivateConfig()
    {
        using var workspace = new TemporaryDirectory("xke-private-cloud-config");
        var configPath = Path.Combine(workspace.RootPath, "cloud.local.json");
        File.WriteAllText(configPath, """
        {
          "notice_base_url": "https://notice.example.test/xke-cloud/",
          "telemetry": {
            "base_url": "https://telemetry.example.test/xke"
          }
        }
        """);
        var previousConfig = Environment.GetEnvironmentVariable(PrivateCloudEndpointOptions.ConfigPathEnvironmentVariable);
        var previousNotice = Environment.GetEnvironmentVariable(HttpNoticeRemoteSource.BaseUrlEnvironmentVariable);
        var previousCloudNotice = Environment.GetEnvironmentVariable("XKE_CLOUD_NOTICE_BASE_URL");
        var previousTelemetry = Environment.GetEnvironmentVariable("XKE_TELEMETRY_CLOUD_ENDPOINT");
        var previousCloudFunction = Environment.GetEnvironmentVariable("XKE_CLOUD_FUNCTION_BASE_URL");

        try
        {
            Environment.SetEnvironmentVariable(PrivateCloudEndpointOptions.ConfigPathEnvironmentVariable, configPath);
            Environment.SetEnvironmentVariable(HttpNoticeRemoteSource.BaseUrlEnvironmentVariable, null);
            Environment.SetEnvironmentVariable("XKE_CLOUD_NOTICE_BASE_URL", null);
            Environment.SetEnvironmentVariable("XKE_TELEMETRY_CLOUD_ENDPOINT", null);
            Environment.SetEnvironmentVariable("XKE_CLOUD_FUNCTION_BASE_URL", null);

            Assert.Equal("https://notice.example.test/xke-cloud/", HttpNoticeRemoteSource.ResolveBaseUrl());
            Assert.Equal("https://telemetry.example.test/xke", HttpTelemetryTransport.ResolveBaseUrl());
        }
        finally
        {
            Environment.SetEnvironmentVariable(PrivateCloudEndpointOptions.ConfigPathEnvironmentVariable, previousConfig);
            Environment.SetEnvironmentVariable(HttpNoticeRemoteSource.BaseUrlEnvironmentVariable, previousNotice);
            Environment.SetEnvironmentVariable("XKE_CLOUD_NOTICE_BASE_URL", previousCloudNotice);
            Environment.SetEnvironmentVariable("XKE_TELEMETRY_CLOUD_ENDPOINT", previousTelemetry);
            Environment.SetEnvironmentVariable("XKE_CLOUD_FUNCTION_BASE_URL", previousCloudFunction);
        }
    }

    [Fact]
    public async Task UpdatesFeed_FirstCloudPullWritesUnifiedCloudCache()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-cache");
        var remote = new CountingNoticeRemoteSource(BuildRemoteFiles(BuildCloudFeed()));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: remote);

        var response = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&version=1.2.3&channel=beta&trigger=manual");
        var document = await WebHostTestJson.ReadJsonAsync(response);
        var feed = WebHostTestJson.GetData(document).GetProperty("feed");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("cloud", feed.GetProperty("source").GetString());
        Assert.Equal(2, feed.GetProperty("items").GetArrayLength());
        Assert.True(File.Exists(root.CloudFeedPath));
        Assert.True(File.Exists(root.CloudFeedMetaPath));
        Assert.False(Directory.Exists(root.NoticeCacheDirectory));
    }

    [Fact]
    public async Task UpdatesFeed_UsesOneDayCacheForViewTrigger()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-ttl");
        var remote = new CountingNoticeRemoteSource(BuildRemoteFiles(BuildCloudFeed()));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: remote);

        var manual = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        var view = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=view");

        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal(1, remote.ReadCount);
    }

    [Fact]
    public async Task UpdatesFeed_LoadsMarkdownContentAndCachesByItemMetadata()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-content-cache");
        var remote = new CountingNoticeRemoteSource(BuildRemoteFiles(
            BuildCloudFeedWithMarkdownContent("2026-04-25T08:00:00+08:00"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["notice/content/welcome-main.md"] = "# 欢迎\n\n这是公告正文。",
            }));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: remote);

        var manual = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        var manualDocument = await WebHostTestJson.ReadJsonAsync(manual);
        var manualItem = WebHostTestJson.GetData(manualDocument)
            .GetProperty("feed")
            .GetProperty("items")[0];
        var view = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=view");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        var contentFiles = Directory.GetFiles(root.CloudNoticeContentDirectory, "*.md", SearchOption.AllDirectories);

        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        Assert.Equal("# 欢迎\n\n这是公告正文。", manualItem.GetProperty("content").GetString());
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal(1, remote.ReadCountFor("cloud-feed.json"));
        Assert.Equal(1, remote.ReadCountFor("notice/content/welcome-main.md"));
        Assert.Single(contentFiles);
        Assert.True(File.Exists(root.CloudNoticeContentMetaPath));
    }

    [Fact]
    public async Task UpdatesFeed_ReloadsMarkdownWhenItemMetadataChanges()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-content-reload");
        var files = BuildRemoteFiles(
            BuildCloudFeedWithMarkdownContent("2026-04-25T08:00:00+08:00"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["notice/content/welcome-main.md"] = "v1",
            });
        var remote = new CountingNoticeRemoteSource(files);

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: remote);

        var first = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        files["cloud-feed.json"] = JsonSerializer.SerializeToUtf8Bytes(
            BuildCloudFeedWithMarkdownContent("2026-04-25T09:00:00+08:00"),
            new JsonSerializerOptions { WriteIndented = true });
        files["notice/content/welcome-main.md"] = Encoding.UTF8.GetBytes("v2");

        var second = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        var document = await WebHostTestJson.ReadJsonAsync(second);
        var item = WebHostTestJson.GetData(document)
            .GetProperty("feed")
            .GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("v2", item.GetProperty("content").GetString());
        Assert.Equal(2, remote.ReadCountFor("notice/content/welcome-main.md"));
    }

    [Fact]
    public async Task UpdatesFeed_UsesCachedMarkdownWhenRemoteContentFails()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-content-stale");
        var files = BuildRemoteFiles(
            BuildCloudFeedWithMarkdownContent("2026-04-25T08:00:00+08:00"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["notice/content/welcome-main.md"] = "cached-content",
            });
        var remote = new CountingNoticeRemoteSource(files);

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: remote);

        var first = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        files["cloud-feed.json"] = JsonSerializer.SerializeToUtf8Bytes(
            BuildCloudFeedWithMarkdownContent("2026-04-25T09:00:00+08:00"),
            new JsonSerializerOptions { WriteIndented = true });
        files.Remove("notice/content/welcome-main.md");

        var second = await host.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        var document = await WebHostTestJson.ReadJsonAsync(second);
        var feed = WebHostTestJson.GetData(document).GetProperty("feed");
        var item = feed.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("cached-content", item.GetProperty("content").GetString());
        Assert.True(item.GetProperty("content_stale").GetBoolean());
        Assert.Contains(feed.GetProperty("warnings").EnumerateArray(), warning => warning.GetString()?.Contains("公告正文读取失败", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task UpdatesFeed_RemoteFailureReturnsLastGoodCloudCache()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-last-good");

        await using (var firstHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: new CountingNoticeRemoteSource(BuildRemoteFiles(BuildCloudFeed()))))
        {
            var initial = await firstHost.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        }

        await using var secondHost = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: new MissingNoticeRemoteSource());

        var response = await secondHost.Client.GetAsync("/api/updates/feed?student_id=20260001&trigger=manual");
        var document = await WebHostTestJson.ReadJsonAsync(response);
        var feed = WebHostTestJson.GetData(document).GetProperty("feed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("local-cache", feed.GetProperty("source").GetString());
        Assert.True(feed.GetProperty("stale").GetBoolean());
        Assert.Contains(feed.GetProperty("warnings").EnumerateArray(), item => item.GetString()?.Contains("云端入口读取失败", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task CoursePreview_RemoteFeatureFlagDisabledReturns403()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-feature-guard");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        var session = root.Sessions().Create("20260001", "20260001", "test");
        var remote = new CountingNoticeRemoteSource(BuildRemoteFiles(BuildCloudFeed(publicCoursePreview: false)));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: remote);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/course/preview")
        {
            Content = JsonContent.Create(new
            {
                round_id = "round-1",
                refresh = true,
            }),
        };
        request.Headers.Add("X-XKE-Session", session.SessionId);

        var response = await host.Client.SendAsync(request);
        var document = await WebHostTestJson.ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("remote-policy-feature-disabled", document.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task TelemetryReport_WritesLocalOnlyCloudSpool()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-telemetry-spool");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        var session = root.Sessions().Create("20260001", "20260001", "test");

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: new MissingNoticeRemoteSource());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry/report")
        {
            Content = JsonContent.Create(new
            {
                reason = "startup",
                session_started_at = "2026-04-25T08:00:00+08:00",
                foreground_seconds_delta = 12,
                background_seconds_delta = 0,
                active_tab = "dashboard",
                module_hint = "home",
                build = "test-build",
                cloud_upload_enabled = true,
                cloud_skip_reason = string.Empty,
                feature_usage_deltas = new Dictionary<string, object>
                {
                    ["dashboard"] = new Dictionary<string, object>
                    {
                        ["open_count"] = 1,
                        ["foreground_seconds_delta"] = 12,
                    },
                },
            }),
        };
        request.Headers.Add("X-XKE-Session", session.SessionId);

        var response = await host.Client.SendAsync(request);
        var document = await WebHostTestJson.ReadJsonAsync(response);
        var cloud = WebHostTestJson.GetData(document).GetProperty("cloud");
        var spoolFiles = Directory.GetFiles(root.CloudTelemetrySpoolDirectory, "*.json", SearchOption.AllDirectories);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(cloud.GetProperty("queued").GetBoolean());
        Assert.False(cloud.GetProperty("skipped").GetBoolean());
        Assert.Equal("spooled-local-only", cloud.GetProperty("reason").GetString());
        Assert.Single(spoolFiles);
        var encryptedSpool = File.ReadAllText(spoolFiles[0]);
        Assert.StartsWith(LocalEncryptedFileStore.EnvelopePrefix, encryptedSpool);
        Assert.DoesNotContain("20260001", encryptedSpool, StringComparison.Ordinal);

        using var spoolDocument = JsonDocument.Parse(new LocalEncryptedFileStore().ReadBytes(spoolFiles[0]));
        var payload = spoolDocument.RootElement.GetProperty("payload");
        Assert.Equal("20260001", payload.GetProperty("student_id").GetString());
    }

    [Fact]
    public async Task TelemetryReport_UploadsCurrentSpoolToCloudTransportAndClearsItem()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-cloudsync-telemetry-upload");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        var session = root.Sessions().Create("20260001", "20260001", "test");
        var transport = new CapturingTelemetryTransport(new TelemetryCloudResult(
            Ok: true,
            Queued: false,
            Skipped: false,
            Reason: "uploaded"));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-cloudsync",
            noticeRemoteSource: new MissingNoticeRemoteSource(),
            telemetryTransport: transport);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry/report")
        {
            Content = JsonContent.Create(new
            {
                reason = "startup",
                foreground_seconds_delta = 3,
                active_tab = "dashboard",
                module_hint = "home",
                build = "test-build",
                cloud_upload_enabled = true,
                feature_usage_deltas = new Dictionary<string, object>
                {
                    ["dashboard"] = new Dictionary<string, object>
                    {
                        ["open_count"] = 1,
                        ["foreground_seconds_delta"] = 3,
                    },
                },
            }),
        };
        request.Headers.Add("X-XKE-Session", session.SessionId);

        var response = await host.Client.SendAsync(request);
        var document = await WebHostTestJson.ReadJsonAsync(response);
        var cloud = WebHostTestJson.GetData(document).GetProperty("cloud");
        var spoolFiles = Directory.GetFiles(root.CloudTelemetrySpoolDirectory, "*.json", SearchOption.AllDirectories);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(cloud.GetProperty("queued").GetBoolean());
        Assert.False(cloud.GetProperty("skipped").GetBoolean());
        Assert.Equal("uploaded", cloud.GetProperty("reason").GetString());
        Assert.Empty(spoolFiles);
        Assert.Single(transport.Payloads);
        var payload = (Dictionary<string, object?>)transport.Payloads[0]["payload"]!;
        Assert.Equal("20260001", payload["student_id"]?.ToString());
    }

    [Fact]
    public void RemotePolicy_NormalizesBadFieldsAndClampsFeatureLimits()
    {
        var service = new RemotePolicyService();

        var config = service.NormalizePolicy(new Dictionary<string, object?>
        {
            ["version_policy"] = new Dictionary<string, object?>
            {
                ["versions"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["feature_flags"] = new Dictionary<string, object?>
                        {
                            ["schedule_view"] = "bad",
                            ["public_course_preview"] = false,
                        },
                        ["feature_limits"] = new Dictionary<string, object?>
                        {
                            ["course_grab"] = new Dictionary<string, object?>
                            {
                                ["min_interval_sec"] = 0.01d,
                                ["max_runtime_sec"] = 999999,
                                ["cooldown_sec"] = -10,
                            },
                        },
                        ["runtime_policy"] = new Dictionary<string, object?>
                        {
                            ["config_pull_interval_sec"] = 1,
                        },
                    },
                },
            },
        }, new Dictionary<string, object?>());

        var flags = (Dictionary<string, object?>)config["feature_flags"]!;
        var limits = (Dictionary<string, object?>)config["feature_limits"]!;
        var courseGrab = (Dictionary<string, object?>)limits["course_grab"]!;
        var runtimePolicy = (Dictionary<string, object?>)config["runtime_policy"]!;

        Assert.True((bool)flags["schedule_view"]!);
        Assert.False((bool)flags["public_course_preview"]!);
        Assert.Equal(0.5d, (double)courseGrab["min_interval_sec"]!);
        Assert.Equal(3600, (int)courseGrab["max_runtime_sec"]!);
        Assert.Equal(0, (int)courseGrab["cooldown_sec"]!);
        Assert.Equal(300, (int)runtimePolicy["config_pull_interval_sec"]!);
    }

    [Fact]
    public void RemotePolicy_NormalizesReleaseNotesAsSingleTextField()
    {
        var service = new RemotePolicyService();
        var longText = new string('x', 5000);

        var config = service.NormalizePolicy([], new Dictionary<string, object?>
        {
            ["release_notes"] = "第一行更新说明\n第二行更新说明",
        });
        var updateInfo = (Dictionary<string, object?>)config["update_info"]!;

        Assert.Equal("第一行更新说明\n第二行更新说明", updateInfo["release_notes"]);
        Assert.Equal(updateInfo["release_notes"], config["release_notes"]);

        var badConfig = service.NormalizePolicy([], new Dictionary<string, object?>
        {
            ["release_notes"] = new Dictionary<string, object?> { ["summary"] = "不再支持对象结构" },
        });
        var badUpdateInfo = (Dictionary<string, object?>)badConfig["update_info"]!;
        Assert.Equal(string.Empty, badUpdateInfo["release_notes"]);

        var longConfig = service.NormalizePolicy([], new Dictionary<string, object?>
        {
            ["release_notes"] = longText,
        });
        var longUpdateInfo = (Dictionary<string, object?>)longConfig["update_info"]!;
        Assert.Equal(4000, ((string)longUpdateInfo["release_notes"]!).Length);
    }

    private static Dictionary<string, byte[]> BuildRemoteFiles(Dictionary<string, object?> feed)
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cloud-feed.json"] = JsonSerializer.SerializeToUtf8Bytes(feed, new JsonSerializerOptions { WriteIndented = true }),
        };
    }

    private static Dictionary<string, byte[]> BuildRemoteFiles(Dictionary<string, object?> feed, Dictionary<string, string> markdownFiles)
    {
        var files = BuildRemoteFiles(feed);
        foreach (var (path, content) in markdownFiles)
        {
            files[path] = Encoding.UTF8.GetBytes(content);
        }
        return files;
    }

    private static Dictionary<string, object?> BuildCloudFeed(
        bool publicCoursePreview = true,
        bool courseGrab = false)
    {
        const string updatedAt = "2026-04-25T08:00:00+08:00";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["announcement"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "测试公告",
                ["subtitle"] = "CloudSync",
                ["notice_text"] = "云端单包公告",
                ["items"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["id"] = "panel-1",
                        ["title"] = "主公告",
                        ["summary"] = "面板公告",
                        ["display_mode"] = "panel",
                        ["severity"] = "info",
                        ["updated_at"] = updatedAt,
                    },
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["id"] = "reminder-1",
                        ["title"] = "提醒",
                        ["summary"] = "提醒公告",
                        ["display_mode"] = "reminder",
                        ["severity"] = "important",
                        ["updated_at"] = updatedAt,
                    },
                },
            },
            ["policy"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["access_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["default_message"] = "该账号当前不可用",
                    ["blocked_students"] = new List<object?>(),
                    ["blocked_student_hashes"] = new List<object?>(),
                },
                ["version_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["versions"] = new List<object?>
                    {
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["id"] = "beta",
                            ["version"] = "1.2.3",
                            ["enabled"] = true,
                            ["app_control"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["state"] = "normal",
                                ["reason"] = string.Empty,
                            },
                            ["feature_flags"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["schedule_view"] = true,
                                ["public_course_preview"] = publicCoursePreview,
                                ["course_grab"] = courseGrab,
                                ["campus_login"] = true,
                                ["keepalive"] = true,
                            },
                            ["feature_limits"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["course_grab"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    ["min_interval_sec"] = 0.5d,
                                    ["max_runtime_sec"] = 5,
                                    ["cooldown_sec"] = 86400,
                                },
                            },
                            ["runtime_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["config_pull_interval_sec"] = 86400,
                                ["telemetry_upload_interval_sec"] = 21600,
                            },
                        },
                    },
                },
            },
            ["update_info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updated_at"] = updatedAt,
                ["latest_version"] = "1.2.3",
                ["min_supported_version"] = "1.0.0",
                ["download_url"] = "https://example.invalid/xke",
                ["force_upgrade"] = false,
            },
        };
    }

    private static Dictionary<string, object?> BuildCloudFeedWithMarkdownContent(string itemUpdatedAt)
    {
        var feed = BuildCloudFeed();
        var announcement = (Dictionary<string, object?>)feed["announcement"]!;
        announcement["items"] = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "welcome-main",
                ["group"] = "welcome-20260319",
                ["group_title"] = "欢迎公告集",
                ["title"] = "欢迎使用",
                ["summary"] = "公告摘要",
                ["kind"] = "announcement",
                ["severity"] = "info",
                ["pinned"] = true,
                ["display_mode"] = "modal",
                ["updated_at"] = itemUpdatedAt,
                ["content_path"] = "notice/content/welcome-main.md",
            },
        };
        feed["updated_at"] = itemUpdatedAt;
        return feed;
    }

    private sealed class CountingNoticeRemoteSource : INoticeRemoteSource
    {
        private readonly Dictionary<string, byte[]> _files;
        private readonly Dictionary<string, int> _readCounts = new(StringComparer.Ordinal);

        public CountingNoticeRemoteSource(Dictionary<string, byte[]> files)
        {
            _files = files;
        }

        public int ReadCount { get; private set; }

        public int ReadCountFor(string relativePath)
        {
            var normalized = (relativePath ?? string.Empty).Trim().TrimStart('/').Replace('\\', '/');
            return _readCounts.GetValueOrDefault(normalized);
        }

        public Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            var normalized = (relativePath ?? string.Empty).Trim().TrimStart('/').Replace('\\', '/');
            _readCounts[normalized] = _readCounts.GetValueOrDefault(normalized) + 1;
            return _files.TryGetValue(normalized, out var content)
                ? Task.FromResult(new NoticeRemoteContentResult(true, content, string.Empty))
                : Task.FromResult(new NoticeRemoteContentResult(false, Array.Empty<byte>(), $"missing: {normalized}"));
        }
    }

    private sealed class CapturingTelemetryTransport : ITelemetryTransport
    {
        private readonly TelemetryCloudResult _result;

        public CapturingTelemetryTransport(TelemetryCloudResult result)
        {
            _result = result;
        }

        public List<Dictionary<string, object?>> Payloads { get; } = [];

        public Task<TelemetryCloudResult> ReportAsync(
            Dictionary<string, object?> payload,
            string skipReason,
            CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(_result);
        }
    }
}
