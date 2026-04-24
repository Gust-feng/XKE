using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Notice;
using Xke.Core.Schedule;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleWebHostTests
{
    [Fact]
    public async Task ScheduleEndpoints_RefreshPreferredCacheAndLoadLocal()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-data");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: fakeAuth,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginData = GetData(await ReadJsonAsync(login));
        var sessionId = loginData.GetProperty("session_id").GetString()!;
        Assert.Equal("live_fetch", loginData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal("第7周", loginData.GetProperty("cached_schedule").GetProperty("week_label").GetString());
        Assert.Equal(1, fakeSchedule.PreferredCalls);

        var refresh = await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var refreshDoc = await ReadJsonAsync(refresh);
        var refreshData = GetData(refreshDoc);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal("live_fetch", refreshData.GetProperty("data_origin").GetString());
        Assert.Equal("mobile", refreshData.GetProperty("source_type").GetString());
        Assert.Equal("第7周", refreshData.GetProperty("week_label").GetString());
        Assert.Equal("信息工程学院", refreshData.GetProperty("student").GetProperty("academy_name").GetString());
        Assert.Equal("软件技术", refreshData.GetProperty("student").GetProperty("major_name").GetString());
        Assert.Equal("专科", refreshData.GetProperty("student").GetProperty("training_level").GetString());
        Assert.True(refreshData.GetProperty("saved_files").GetArrayLength() >= 2);
        Assert.Equal(2, fakeSchedule.PreferredCalls);
        Assert.Equal("mobile-token-initial", fakeSchedule.LastExistingToken);

        using var localRequest = new HttpRequestMessage(HttpMethod.Get, "/api/schedule/local?week_label=第7周&source=preferred");
        localRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var local = await client.SendAsync(localRequest);
        var localDoc = await ReadJsonAsync(local);
        var localData = GetData(localDoc);

        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.True(localData.GetProperty("cached").GetBoolean());
        Assert.Equal("local_cache", localData.GetProperty("data_origin").GetString());
        Assert.Equal("高等数学", localData.GetProperty("rows")[0].GetProperty("course_name").GetString());
        Assert.Equal("信息工程学院", localData.GetProperty("student").GetProperty("academy_name").GetString());
        Assert.Equal("软件技术", localData.GetProperty("student").GetProperty("major_name").GetString());
        Assert.Equal("专科", localData.GetProperty("student").GetProperty("training_level").GetString());
    }

    [Fact]
    public async Task ScheduleEndpoints_CookieRouteIsDeferredAndPreferredStaysMobileOnly()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-cookie");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: fakeAuth,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginData = GetData(await ReadJsonAsync(login));
        var sessionId = loginData.GetProperty("session_id").GetString()!;
        Assert.True(loginData.GetProperty("connected").GetBoolean());
        Assert.Equal("live_fetch", loginData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());

        var cookieDenied = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var cookieDeniedDoc = await ReadJsonAsync(cookieDenied);

        Assert.Equal(HttpStatusCode.Conflict, cookieDenied.StatusCode);
        Assert.False(cookieDeniedDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("教务系统验证码登录暂未接入，请先使用移动教务课表", cookieDeniedDoc.RootElement.GetProperty("msg").GetString());
        Assert.Equal("教务系统验证码登录暂未接入，请先使用移动教务课表", cookieDeniedDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, fakeSchedule.CookieCalls);

        fakeSchedule.FailPreferredOnce = true;
        var fallback = await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
            CookieRq = "2026-04-20",
        });
        var fallbackDoc = await ReadJsonAsync(fallback);

        Assert.Equal(HttpStatusCode.InternalServerError, fallback.StatusCode);
        Assert.False(fallbackDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("移动教务暂不可用", fallbackDoc.RootElement.GetProperty("msg").GetString());
        Assert.Equal("移动教务暂不可用", fallbackDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, fakeSchedule.CookieCalls);
    }

    [Fact]
    public async Task ScheduleEndpoints_RestoreLocalAcrossRestartAndResetLiveCookieConnection()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-restart");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient();

        async Task<XkeWebHostService> CreateServiceAsync(IJwScheduleClient scheduleClient)
        {
            var service = new XkeWebHostService(new XkeWebHostOptions
            {
                HostMode = "test-schedule",
                RuntimeChannel = "dotnet8-test",
                WebRootPath = webRoot.RootPath,
                AuthDataRootPath = dataRoot.RootPath,
                JwAuthClient = fakeAuth,
                JwScheduleClient = scheduleClient,
                NoticeRemoteSource = new MissingNoticeRemoteSource(),
                Urls = new[] { "http://127.0.0.1:0" },
            });
            await service.StartAsync();
            return service;
        }

        await using var firstService = await CreateServiceAsync(fakeSchedule);
        string sessionId;
        using (var firstClient = new HttpClient { BaseAddress = new Uri(firstService.LaunchResult!.BaseUrl) })
        {
            var login = await firstClient.PostAsJsonAsync("/api/auth/login", new
            {
                user = "20260001",
                password = "secret",
                prelogin_mode = "capture",
            });
            sessionId = GetData(await ReadJsonAsync(login)).GetProperty("session_id").GetString()!;
        }

        await firstService.StopAsync();

        await using var secondService = await CreateServiceAsync(new FakeJwScheduleClient());
        using var secondClient = new HttpClient { BaseAddress = new Uri(secondService.LaunchResult!.BaseUrl) };

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var sessionResponse = await secondClient.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.False(GetData(sessionDoc).GetProperty("connected").GetBoolean());

        using var localRequest = new HttpRequestMessage(HttpMethod.Get, "/api/schedule/local?source=preferred");
        localRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var local = await secondClient.SendAsync(localRequest);
        Assert.Equal(HttpStatusCode.OK, local.StatusCode);

        var cookieDenied = await secondClient.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var cookieDeniedDoc = await ReadJsonAsync(cookieDenied);
        Assert.Equal(HttpStatusCode.Conflict, cookieDenied.StatusCode);
        Assert.Equal("教务系统验证码登录暂未接入，请先使用移动教务课表", cookieDeniedDoc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BlockedStudent_ScheduleEndpointReturns403Detail()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-blocked");
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
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new FakeJwScheduleClient(),
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/schedule/local?source=preferred");
        request.Headers.Add(SessionConstants.SessionHeaderName, session.SessionId);
        var response = await client.SendAsync(request);
        var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("detail").GetProperty("blocked").GetBoolean());
        Assert.Equal("20269999", document.RootElement.GetProperty("detail").GetProperty("student_id").GetString());
    }

    [Fact]
    public async Task ScheduleEndpoints_ExposeWarningsFromNormalizedMobilePayload()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-warnings");
        var fakeSchedule = new FakeJwScheduleClient
        {
            IncludeMissingSectionWarning = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = GetData(await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        }))).GetProperty("session_id").GetString()!;

        var refreshData = GetData(await ReadJsonAsync(await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        })));

        Assert.Equal(1, refreshData.GetProperty("data_warnings").GetArrayLength());
        Assert.Equal("mobile-course-section-missing", refreshData.GetProperty("data_warnings")[0].GetProperty("type").GetString());
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

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
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

    private sealed class FakeJwScheduleClient : IJwScheduleClient
    {
        public bool FailPreferredOnce { get; set; }
        public bool IncludeMissingSectionWarning { get; set; }
        public int PreferredCalls { get; private set; }
        public int CookieCalls { get; private set; }
        public string LastExistingToken { get; private set; } = string.Empty;

        public Task<JwScheduleFetchResult> FetchPreferredAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            PreferredCalls++;
            LastExistingToken = existingToken;
            if (FailPreferredOnce)
            {
                FailPreferredOnce = false;
                throw new JwScheduleException("移动教务暂不可用");
            }

            return Task.FromResult(BuildResult("mobile_schedule", "移动教务课表", "mobile", 7, "移动教务课表刷新成功", IncludeMissingSectionWarning));
        }

        public Task<JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            CookieCalls++;
            return Task.FromResult(BuildResult("jw_schedule", "教务系统课表", "jw", 8, "Cookie 课表刷新成功"));
        }

        private static JwScheduleFetchResult BuildResult(string sourceKey, string sourceLabel, string sourceType, int weekNumber, string remarks, bool includeWarning = false)
        {
            var weekLabel = $"第{weekNumber}周";
            return new JwScheduleFetchResult(
                SourceKey: sourceKey,
                SourceLabel: sourceLabel,
                SourceType: sourceType,
                WeekLabel: weekLabel,
                CurrentWeekNumber: weekNumber,
                ViewedWeekNumber: weekNumber,
                AvailableWeeks:
                [
                    new Dictionary<string, object?>
                    {
                        ["value"] = weekNumber.ToString(),
                        ["label"] = weekLabel,
                        ["selected"] = true,
                    },
                ],
                WeekDates: new Dictionary<string, string>
                {
                    ["星期一"] = "2026-04-20",
                    ["星期日"] = "2026-04-26",
                },
                BlockTimes:
                [
                    new Dictionary<string, object?>
                    {
                        ["block_label"] = "上午",
                        ["section"] = "第1-2节",
                        ["block_time"] = "08:00-09:35",
                        ["small_section_1"] = "08:00-08:45",
                        ["small_section_2"] = "08:50-09:35",
                        ["break_minutes"] = 5,
                    },
                ],
                DataWarnings: includeWarning
                    ? [
                        new Dictionary<string, object?>
                        {
                            ["type"] = "mobile-course-section-missing",
                            ["message"] = "高等数学 缺少节次字段",
                        },
                    ]
                    : [
                        new Dictionary<string, object?>
                        {
                            ["type"] = "duplicate_slot",
                            ["course_name"] = "高等数学",
                            ["week_label"] = weekLabel,
                            ["weekday"] = "星期一",
                            ["kept_room"] = "1-101",
                            ["kept_weeks"] = weekLabel,
                            ["discarded"] = new List<object?>(),
                            ["message"] = "检测到冲突",
                        },
                    ],
                Rows:
                [
                    new Dictionary<string, object?>
                    {
                        ["source"] = sourceType == "jw" ? "Cookie" : "加密",
                        ["week_label"] = weekLabel,
                        ["weekday"] = "星期一",
                        ["section"] = "第1-2节",
                        ["time_text"] = "星期一 / 第1-2节 / 08:00-09:35",
                        ["time_range"] = "08:00-09:35",
                        ["block_time"] = string.Empty,
                        ["small_section_1"] = string.Empty,
                        ["small_section_2"] = string.Empty,
                        ["break_minutes"] = 0,
                        ["course_name"] = "高等数学",
                        ["teacher"] = "张老师",
                        ["room"] = "1-101",
                        ["weeks"] = weekLabel,
                        ["credit"] = "4",
                    },
                ],
                RawPayload: new Dictionary<string, object?>
                {
                    ["source"] = sourceType,
                    ["week"] = weekNumber,
                },
                Remarks: remarks,
                FallbackReason: string.Empty);
        }
    }

}
