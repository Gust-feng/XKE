using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth.Jw;
using Xke.Core.Course;
using Xke.Core.Notice;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class CourseWebHostTests
{
    [Fact]
    public async Task CourseEndpoints_ProductionAdapterRequiresTraditionalJwConnectionAndDefersGrab()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-course-webhost-deferred");

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-course",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient());
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");

        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/api/course/status");
        statusRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var status = await client.SendAsync(statusRequest);
        var statusData = GetData(await ReadJsonAsync(status));

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.False(statusData.GetProperty("course_connection").GetProperty("connected").GetBoolean());
        Assert.Equal("disconnected", statusData.GetProperty("course_connection").GetProperty("status").GetString());
        Assert.Equal("course_connection_required", statusData.GetProperty("course_connection").GetProperty("reason").GetString());
        Assert.False(statusData.GetProperty("features").GetProperty("grab").GetBoolean());

        var connect = await client.PostAsJsonAsync($"/api/course/connect?session_id={sessionId}", new { });
        var connectDoc = await ReadJsonAsync(connect);
        Assert.Equal(HttpStatusCode.Conflict, connect.StatusCode);
        Assert.False(connectDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("course_connection_required", connectDoc.RootElement.GetProperty("reason").GetString());

        var rounds = await client.GetAsync($"/api/course/rounds?session_id={sessionId}");
        var roundsDoc = await ReadJsonAsync(rounds);
        Assert.Equal(HttpStatusCode.Conflict, rounds.StatusCode);
        Assert.Equal("course_connection_required", roundsDoc.RootElement.GetProperty("reason").GetString());

        var grab = await client.PostAsJsonAsync($"/api/course/grab/start?session_id={sessionId}", new CourseGrabStartRequest
        {
            RoundId = "round-1",
            CourseId = "course-1001",
            SectionId = "section-2002",
            Interval = 0.8,
        });
        var grabData = GetData(await ReadJsonAsync(grab));
        Assert.Equal(HttpStatusCode.NotImplemented, grab.StatusCode);
        Assert.Equal("course-grab-deferred", grabData.GetProperty("reason").GetString());
        Assert.True(grabData.GetProperty("grab_state").GetProperty("deferred").GetBoolean());
    }

    [Fact]
    public async Task CourseEndpoints_FakeClientConnectsLoadsRoundsPreviewAndCache()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-course-webhost-fake");
        var fakeCourse = new FakeCourseClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-course",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(),
            courseClient: fakeCourse);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");

        var denied = await client.GetAsync($"/api/course/rounds?session_id={sessionId}");
        var deniedDoc = await ReadJsonAsync(denied);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal("course_connection_required", deniedDoc.RootElement.GetProperty("reason").GetString());

        var connect = await client.PostAsJsonAsync($"/api/course/connect?session_id={sessionId}", new { });
        var connectData = GetData(await ReadJsonAsync(connect));
        Assert.Equal(HttpStatusCode.OK, connect.StatusCode);
        Assert.True(connectData.GetProperty("course_connection").GetProperty("connected").GetBoolean());
        Assert.Equal(1, fakeCourse.ConnectCalls);

        using var roundsRefreshRequest = new HttpRequestMessage(HttpMethod.Get, "/api/course/rounds?refresh=true");
        roundsRefreshRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var roundsRefresh = await client.SendAsync(roundsRefreshRequest);
        var roundsRefreshData = GetData(await ReadJsonAsync(roundsRefresh));
        Assert.Equal(HttpStatusCode.OK, roundsRefresh.StatusCode);
        Assert.False(roundsRefreshData.GetProperty("cached").GetBoolean());
        Assert.Equal("round-1", roundsRefreshData.GetProperty("rounds")[0].GetProperty("round_id").GetString());
        Assert.Equal(1, fakeCourse.RoundsCalls);

        var roundsCached = await client.GetAsync($"/api/course/rounds?session_id={sessionId}");
        var roundsCachedData = GetData(await ReadJsonAsync(roundsCached));
        Assert.Equal(HttpStatusCode.OK, roundsCached.StatusCode);
        Assert.True(roundsCachedData.GetProperty("cached").GetBoolean());
        Assert.Equal(1, fakeCourse.RoundsCalls);

        var previewRefresh = await client.PostAsJsonAsync($"/api/course/preview?session_id={sessionId}", new CoursePreviewRequest
        {
            RoundId = "round-1",
            Refresh = true,
        });
        var previewRefreshData = GetData(await ReadJsonAsync(previewRefresh));
        Assert.Equal(HttpStatusCode.OK, previewRefresh.StatusCode);
        Assert.False(previewRefreshData.GetProperty("cached").GetBoolean());
        Assert.Equal("大学美育", previewRefreshData.GetProperty("courses")[0].GetProperty("kcmc").GetString());
        Assert.Equal(1, fakeCourse.PreviewCalls);

        using var previewCachedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/course/preview");
        previewCachedRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        previewCachedRequest.Content = JsonContent.Create(new CoursePreviewRequest
        {
            RoundId = "round-1",
            Refresh = false,
        });
        var previewCached = await client.SendAsync(previewCachedRequest);
        var previewCachedData = GetData(await ReadJsonAsync(previewCached));
        Assert.Equal(HttpStatusCode.OK, previewCached.StatusCode);
        Assert.True(previewCachedData.GetProperty("cached").GetBoolean());
        Assert.Equal(1, fakeCourse.PreviewCalls);
    }

    [Fact]
    public async Task CourseEndpoints_ClearsConnectionWhenRuntimeSessionExpires()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-course-webhost-expired");
        var fakeCourse = new FakeCourseClient
        {
            ExpireOnRounds = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-course",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(),
            courseClient: fakeCourse);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");

        var connect = await client.PostAsJsonAsync($"/api/course/connect?session_id={sessionId}", new { });
        Assert.Equal(HttpStatusCode.OK, connect.StatusCode);

        var rounds = await client.GetAsync($"/api/course/rounds?refresh=true&session_id={sessionId}");
        var roundsDoc = await ReadJsonAsync(rounds);
        Assert.Equal(HttpStatusCode.Conflict, rounds.StatusCode);
        Assert.Equal("course_connection_required", roundsDoc.RootElement.GetProperty("reason").GetString());

        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/api/course/status");
        statusRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var status = await client.SendAsync(statusRequest);
        var statusData = GetData(await ReadJsonAsync(status));
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.False(statusData.GetProperty("course_connection").GetProperty("connected").GetBoolean());
        Assert.Equal("disconnected", statusData.GetProperty("course_connection").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CourseConnection_DoesNotSurviveWebHostRestart()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-course-webhost-restart");

        async Task<XkeWebHostService> CreateServiceAsync(ICourseClient courseClient)
        {
            var service = new XkeWebHostService(new XkeWebHostOptions
            {
                HostMode = "test-course",
                RuntimeChannel = "dotnet8-test",
                WebRootPath = webRoot.RootPath,
                AuthDataRootPath = dataRoot.RootPath,
                JwAuthClient = new FakeJwAuthClient(),
                UseLegacyMobileAuthentication = true,
                UseLegacyMobileScheduleForTests = true,
                JwScheduleClient = new DefaultFakeJwScheduleClient(),
                CourseClient = courseClient,
                NoticeRemoteSource = new MissingNoticeRemoteSource(),
                Urls = new[] { "http://127.0.0.1:0" },
            });
            await service.StartAsync();
            return service;
        }

        string sessionId;
        await using (var firstService = await CreateServiceAsync(new FakeCourseClient()))
        {
            using var firstClient = new HttpClient { BaseAddress = new Uri(firstService.LaunchResult!.BaseUrl) };
            sessionId = await LoginAsync(firstClient, "20260001");
            var connect = await firstClient.PostAsJsonAsync($"/api/course/connect?session_id={sessionId}", new { });
            Assert.Equal(HttpStatusCode.OK, connect.StatusCode);
            var refresh = await firstClient.GetAsync($"/api/course/rounds?refresh=true&session_id={sessionId}");
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        }

        await using var secondService = await CreateServiceAsync(new FakeCourseClient());
        using var secondClient = new HttpClient { BaseAddress = new Uri(secondService.LaunchResult!.BaseUrl) };
        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/api/course/status");
        statusRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var status = await secondClient.SendAsync(statusRequest);
        var statusData = GetData(await ReadJsonAsync(status));
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.False(statusData.GetProperty("course_connection").GetProperty("connected").GetBoolean());

        var rounds = await secondClient.GetAsync($"/api/course/rounds?session_id={sessionId}");
        var roundsDoc = await ReadJsonAsync(rounds);
        Assert.Equal(HttpStatusCode.Conflict, rounds.StatusCode);
        Assert.Equal("course_connection_required", roundsDoc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task BlockedStudent_CourseEndpointReturns403Detail()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-course-webhost-blocked");
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
            hostMode: "test-course",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new DefaultFakeJwScheduleClient(),
            courseClient: new FakeCourseClient(),
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/course/status");
        request.Headers.Add(SessionConstants.SessionHeaderName, session.SessionId);
        var response = await client.SendAsync(request);
        var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("detail").GetProperty("blocked").GetBoolean());
        Assert.Equal("20269999", document.RootElement.GetProperty("detail").GetProperty("student_id").GetString());
    }

    private static async Task<string> LoginAsync(HttpClient client, string user)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user,
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginDoc = await ReadJsonAsync(login);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return GetData(loginDoc).GetProperty("session_id").GetString()!;
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

    private sealed class FakeCourseClient : ICourseClient
    {
        public bool IsAvailable => true;
        public string UnavailableReason => string.Empty;
        public string UnavailableMessage => string.Empty;
        public bool ExpireOnRounds { get; init; }
        public bool ExpireOnPreview { get; init; }
        public int ConnectCalls { get; private set; }
        public int RoundsCalls { get; private set; }
        public int PreviewCalls { get; private set; }

        public Task ConnectAsync(string sessionId, string studentId, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            return Task.CompletedTask;
        }

        public Task<CourseRoundsFetchResult> LoadRoundsAsync(string sessionId, string studentId, bool refresh, CancellationToken cancellationToken = default)
        {
            RoundsCalls++;
            if (ExpireOnRounds)
            {
                throw new CourseException(409, "传统教务会话已失效，请重新连接教务系统", "course_connection_required");
            }

            return Task.FromResult(new CourseRoundsFetchResult(
                Rounds:
                [
                    new CourseRound(
                        RoundId: "round-1",
                        Name: "公共选修课第一轮",
                        Term: "2025-2026-2",
                        TimeRange: "2026-04-23 ~ 2026-04-25",
                        Status: "open"),
                ],
                Warnings: ["fake-course-client"],
                RawPayload: new Dictionary<string, object?>
                {
                    ["student_id"] = studentId,
                    ["source"] = "fake",
                }));
        }

        public Task<CoursePreviewFetchResult> LoadPreviewAsync(string sessionId, string studentId, string roundId, bool refresh, CancellationToken cancellationToken = default)
        {
            PreviewCalls++;
            if (ExpireOnPreview)
            {
                throw new CourseException(409, "传统教务会话已失效，请重新连接教务系统", "course_connection_required");
            }

            return Task.FromResult(new CoursePreviewFetchResult(
                RoundId: roundId,
                Courses:
                [
                    new CourseItem
                    {
                        Kch = "A1001",
                        Kcmc = "大学美育",
                        Xf = "2",
                        Skls = "李老师",
                        Sksj = "周一 1-2 节",
                        Skdd = "教学楼 101",
                        Xqmc = "主校区",
                        Xkrs = "30",
                        Syrs = "12",
                        Ctsm = string.Empty,
                        Szkcflmc = "公共选修",
                        Jx02Id = "course-1001",
                        Jx0404Id = "section-2002",
                    },
                ],
                Warnings: ["fake-course-client"],
                RawPayload: new Dictionary<string, object?>
                {
                    ["student_id"] = studentId,
                    ["round_id"] = roundId,
                    ["source"] = "fake",
                }));
        }
    }

}
