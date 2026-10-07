using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xunit;

namespace Xke.Contracts.Tests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void ApiEnvelope_UsesLegacyEnvelopeFieldNames()
    {
        var json = JsonSerializer.Serialize(new ApiEnvelope(Msg: "ready", Data: new { channel = "desktop-shell" }));

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"msg\":\"ready\"", json);
        Assert.Contains("\"data\":", json);
        Assert.DoesNotContain("\"Ok\":", json);
        Assert.DoesNotContain("\"Msg\":", json);
        Assert.DoesNotContain("\"Data\":", json);
    }

    [Fact]
    public void HostHealthResponse_UsesSnakeCaseForDiagnostics()
    {
        var payload = new HostHealthResponse(
            Status: "ok",
            Sessions: 1,
            WebRootFound: true,
            WebRoot: "Z:/XKE/web",
            BaseUrl: "http://127.0.0.1:5000",
            FrontendBuild: "20260424-runtime-diagnostics1",
            StaticAssetHashes: new Dictionary<string, string>
            {
                ["assets/app.js"] = "abc123",
            });

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"frontend_build\":\"20260424-runtime-diagnostics1\"", json);
        Assert.Contains("\"static_asset_hashes\":", json);
        Assert.Contains("\"assets/app.js\":\"abc123\"", json);
        Assert.DoesNotContain("\"FrontendBuild\":", json);
        Assert.DoesNotContain("\"StaticAssetHashes\":", json);
    }

    [Fact]
    public void NetworkConfigRequest_UsesSnakeCaseForBaseAndDerivedFields()
    {
        var payload = new NetworkConfigRequest
        {
            Provider = "campus",
            HeartbeatEnabled = true,
            HeartbeatIntervalMinutes = 15,
            CampusIp = "10.0.0.2",
            CampusSubnetPrefix = 24,
            CampusGateway = "10.0.0.1",
            CampusDns = "223.5.5.5",
            TelecomAccount = "telecom-user",
            TelecomPassword = "telecom-pass",
        };

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"provider\":\"campus\"", json);
        Assert.Contains("\"heartbeat_enabled\":true", json);
        Assert.Contains("\"heartbeat_interval_minutes\":15", json);
        Assert.Contains("\"campus_ip\":\"10.0.0.2\"", json);
        Assert.Contains("\"campus_subnet_prefix\":24", json);
        Assert.Contains("\"campus_gateway\":\"10.0.0.1\"", json);
        Assert.Contains("\"campus_dns\":\"223.5.5.5\"", json);
        Assert.Contains("\"telecom_account\":\"telecom-user\"", json);
        Assert.Contains("\"telecom_password\":\"telecom-pass\"", json);
        Assert.DoesNotContain("\"Provider\":", json);
        Assert.DoesNotContain("\"HeartbeatEnabled\":", json);
    }

    [Fact]
    public void ProfileRequest_UsesSnakeCaseForNicknameFields()
    {
        var payload = new ProfileRequest
        {
            Nickname = "阿科",
            UseNickname = true,
        };

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"nickname\":", json);
        Assert.Contains("\"use_nickname\":true", json);
        Assert.DoesNotContain("\"UseNickname\":", json);
    }

    [Fact]
    public void AuthPayload_UsesSnakeCaseForNestedContracts()
    {
        var payload = new AuthPayload(
            SessionId: "sid-1",
            Student: new AuthStudentSummary(
                StudentId: "20260001",
                Name: "测试同学",
                Nickname: "阿科",
                UseNickname: true,
                ClassName: "软件一班",
                MajorName: "软件技术",
                AcademyName: "信息工程学院",
                TrainingLevel: "专科",
                CampusName: "主校区",
                AvatarUrl: "/api/auth/avatar?v=1",
                UpdatedAt: "2026-09-08T00:00:00Z"),
            Preferences: new LegacyPreferencesDocument
            {
                Theme = "forest-notes",
            },
            Settings: new AppSettingsDocument
            {
                Appearance = new AppearanceSettingsDocument { Theme = "forest-notes" },
                Schedule = new ScheduleSettingsDocument
                {
                    Source = "preferred",
                    WeekMode = "current",
                    AutoRefreshMode = "daily",
                    AutoRefreshDailyTime = "20:00",
                    AutoRefreshIntervalHours = 4,
                    LessonReminderMinutes = 15,
                    IgnoredWeekendBlocks = new IgnoredWeekendBlocksDocument
                    {
                        Saturday = new[] { 1, 3 },
                        Sunday = new[] { 2 },
                    },
                },
                Auth = new AuthSettingsDocument { PreloginMode = "auto" },
                Network = new NetworkSettingsDocument
                {
                    Provider = "campus",
                    HeartbeatIntervalMinutes = 5,
                    CampusSubnetPrefix = 24,
                },
                Ui = new UiSettingsDocument
                {
                    Panels = new UiPanelsDocument
                    {
                        Schedule = true,
                        LoginMode = false,
                        Appearance = true,
                    },
                },
            },
            StorageDir: ".runtime/data/students/20260001",
            Connected: false,
            PreloginMode: "auto",
            CachedSchedule: null,
            TrustedLoginToken: "trusted-token",
            CourseGrabState: new AuthCourseGrabState(Status: "idle", Running: false));

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"session_id\":\"sid-1\"", json);
        Assert.Contains("\"student_id\":\"20260001\"", json);
        Assert.Contains("\"avatar_url\":\"/api/auth/avatar?v=1\"", json);
        Assert.Contains("\"updated_at\":\"2026-09-08T00:00:00Z\"", json);
        Assert.Contains("\"storage_dir\":\".runtime/data/students/20260001\"", json);
        Assert.Contains("\"prelogin_mode\":\"auto\"", json);
        Assert.Contains("\"ignored_weekend_blocks\":", json);
        Assert.Contains("\"course_grab_state\":", json);
        Assert.DoesNotContain("\"SessionId\":", json);
        Assert.DoesNotContain("\"StudentId\":", json);
    }

    [Fact]
    public void DesktopSettingsPayload_UsesSnakeCaseForResponseFields()
    {
        var payload = new DesktopSettingsPayload(
            Settings: new DesktopSettingsDocument
            {
                LaunchAtStartup = true,
                SilentStartup = false,
                CloseToTray = true,
            },
            StartupRegistered: null);

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"launch_at_startup\":true", json);
        Assert.Contains("\"silent_startup\":false", json);
        Assert.Contains("\"close_to_tray\":true", json);
        Assert.Contains("\"startup_registered\":null", json);
    }

    [Fact]
    public void DesktopActivatePayload_UsesSnakeCaseForResponseFields()
    {
        var payload = new DesktopActivatePayload(
            Activated: true,
            Visible: true,
            WindowState: new Dictionary<string, object?>
            {
                ["state"] = "normal",
                ["available"] = true,
            });

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"activated\":true", json);
        Assert.Contains("\"visible\":true", json);
        Assert.Contains("\"window_state\":", json);
        Assert.Contains("\"available\":true", json);
        Assert.DoesNotContain("\"WindowState\":", json);
    }

    [Fact]
    public void UpdatesFeedPayload_UsesSnakeCaseForStudentId()
    {
        var payload = new UpdatesFeedPayload(
            Feed: new Dictionary<string, object?>
            {
                ["source"] = "local-cache",
            },
            StudentId: "20260001");

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"student_id\":\"20260001\"", json);
        Assert.Contains("\"feed\":", json);
    }

    [Fact]
    public void ScheduleRequest_UsesSnakeCaseForScheduleFetchFields()
    {
        var payload = new ScheduleRequest
        {
            Mode = "manual",
            ManualWeek = "7",
            CookieRq = "2026-04-21",
            TermLabel = "2025-2026-2",
            AcademicTerm = "2025-2026-2",
        };

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"mode\":\"manual\"", json);
        Assert.Contains("\"manual_week\":\"7\"", json);
        Assert.Contains("\"cookie_rq\":\"2026-04-21\"", json);
        Assert.Contains("\"term_label\":\"2025-2026-2\"", json);
        Assert.Contains("\"academic_term\":\"2025-2026-2\"", json);
        Assert.DoesNotContain("\"ManualWeek\":", json);
    }

    [Fact]
    public void ScheduleData_UsesSnakeCaseForNestedFields()
    {
        var payload = new ScheduleData(
            Source: "移动教务课表",
            SourceLabel: "移动教务课表",
            SourceType: "mobile",
            Rows:
            [
                new ScheduleRow
                {
                    Source = "加密",
                    WeekLabel = "第7周",
                    Weekday = "星期一",
                    Section = "第1-2节",
                    TimeText = "星期一 / 第1-2节 / 08:00-09:35",
                    TimeRange = "08:00-09:35",
                    BlockTime = string.Empty,
                    SmallSection1 = string.Empty,
                    SmallSection2 = string.Empty,
                    BreakMinutes = 0,
                    CourseName = "高等数学",
                    Teacher = "张老师",
                    Room = "1-101",
                    Weeks = "第7周",
                    Credit = "4",
                    Conflict = new ScheduleConflict(
                        Kind: "room_conflict",
                        KeptRoom: "1-101",
                        KeptWeeks: "第7周",
                        DiscardedRooms: new[] { "1-102" }),
                },
            ],
            WeekLabel: "第7周",
            AvailableWeeks:
            [
                new ScheduleWeekOption(Value: "7", Label: "第7周", Selected: true),
            ],
            WeekDates: new Dictionary<string, string>
            {
                ["星期一"] = "2026-04-20",
            },
            CurrentWeekLabel: "第7周",
            CurrentWeekNumber: 7,
            ViewedWeekNumber: 7,
            Remarks: "当前显示本地缓存课表",
            BlockTimes:
            [
                new ScheduleBlockTime(
                    BlockLabel: "上午",
                    Section: "第1-2节",
                    BlockTime: "08:00-09:35",
                    SmallSection1: "08:00-08:45",
                    SmallSection2: "08:50-09:35",
                    BreakMinutes: 5),
            ],
            SavedFiles: new[] { ".runtime/data/students/20260001/schedules/current/weeks/第7周/mobile_schedule.json" },
            FallbackReason: string.Empty,
            DataWarnings:
            [
                new ScheduleWarning(
                    Type: "duplicate_slot",
                    CourseName: "高等数学",
                    WeekLabel: "第7周",
                    Weekday: "星期一",
                    KeptRoom: "1-101",
                    KeptWeeks: "第7周",
                    Discarded:
                    [
                        new ScheduleWarningDiscardedItem(Weeks: "第7周", Room: "1-102", Course: string.Empty),
                    ],
                    Message: "检测到冲突"),
            ],
            Term: new ScheduleTermSummary(Label: "2025-2026-2", AcademicTerm: "2025-2026-2"),
            Student: new ScheduleStudentSummary(StudentId: "20260001", Name: "测试同学"),
            Cached: true,
            DataOrigin: "local_cache",
            UpdatedAt: "2026-04-23T12:00:00Z",
            FileUpdatedAt: "2026-04-23T12:00:01Z");

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"source_label\":", json);
        Assert.Contains("\"source_type\":\"mobile\"", json);
        Assert.Contains("\"week_label\":", json);
        Assert.Contains("\"available_weeks\":", json);
        Assert.Contains("\"current_week_number\":7", json);
        Assert.Contains("\"viewed_week_number\":7", json);
        Assert.Contains("\"block_times\":", json);
        Assert.Contains("\"data_warnings\":", json);
        Assert.Contains("\"student_id\":\"20260001\"", json);
        Assert.Contains("\"file_updated_at\":\"2026-04-23T12:00:01Z\"", json);
        Assert.DoesNotContain("\"SourceLabel\":", json);
        Assert.DoesNotContain("\"WeekLabel\":", json);
    }

    [Fact]
    public void CourseRequests_UseSnakeCaseForRoundAndGrabFields()
    {
        var preview = new CoursePreviewRequest
        {
            RoundId = "round-2026-1",
            Refresh = true,
        };
        var grab = new CourseGrabStartRequest
        {
            RoundId = "round-2026-1",
            CourseId = "course-1001",
            SectionId = "section-2002",
            Interval = 0.8,
        };

        var previewJson = JsonSerializer.Serialize(preview);
        var grabJson = JsonSerializer.Serialize(grab);

        Assert.Contains("\"round_id\":\"round-2026-1\"", previewJson);
        Assert.Contains("\"refresh\":true", previewJson);
        Assert.Contains("\"course_id\":\"course-1001\"", grabJson);
        Assert.Contains("\"section_id\":\"section-2002\"", grabJson);
        Assert.Contains("\"interval\":0.8", grabJson);
        Assert.DoesNotContain("\"RoundId\":", previewJson);
        Assert.DoesNotContain("\"CourseId\":", grabJson);
    }

    [Fact]
    public void CourseResponses_UseSnakeCaseForStatusRoundsPreviewAndDeferredState()
    {
        var status = new CourseStatusPayload(
            StudentId: "20260001",
            StorageDir: ".runtime/data/students/20260001/course",
            CourseConnection: new CourseConnectionState(
                Connected: false,
                Status: "deferred",
                Reason: "traditional-jw-captcha-deferred",
                Message: "选课中心框架已接入，传统教务验证码连接后置",
                UpdatedAt: "2026-04-23T12:00:00Z"),
            Features: new CourseFeatureState(
                Rounds: true,
                Preview: true,
                Grab: false,
                GrabReason: "course-grab-deferred"),
            GrabState: new CourseGrabDeferredState(
                Status: "deferred",
                Running: false,
                Deferred: true,
                Reason: "course-grab-deferred",
                Message: "选课中心框架已接入，快速抢课后续单独开发"));
        var rounds = new CourseRoundsPayload(
            Rounds:
            [
                new CourseRound(
                    RoundId: "round-1",
                    Name: "公共选修课第一轮",
                    Term: "2025-2026-2",
                    TimeRange: "2026-04-23 ~ 2026-04-25",
                    Status: "open"),
            ],
            Count: 1,
            Cached: false,
            UpdatedAt: "2026-04-23T12:05:00Z",
            Warnings: ["fixture-source"]);
        var preview = new CoursePreviewPayload(
            RoundId: "round-1",
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
            Count: 1,
            Cached: true,
            UpdatedAt: "2026-04-23T12:06:00Z",
            Warnings: []);
        var deferred = new CourseDeferredPayload(
            Reason: "course-grab-deferred",
            GrabState: status.GrabState);

        var statusJson = JsonSerializer.Serialize(status);
        var roundsJson = JsonSerializer.Serialize(rounds);
        var previewJson = JsonSerializer.Serialize(preview);
        var deferredJson = JsonSerializer.Serialize(deferred);

        Assert.Contains("\"student_id\":\"20260001\"", statusJson);
        Assert.Contains("\"storage_dir\":\".runtime/data/students/20260001/course\"", statusJson);
        Assert.Contains("\"course_connection\":", statusJson);
        Assert.Contains("\"grab_reason\":\"course-grab-deferred\"", statusJson);
        Assert.Contains("\"round_id\":\"round-1\"", roundsJson);
        Assert.Contains("\"time_range\":\"2026-04-23 ~ 2026-04-25\"", roundsJson);
        Assert.Contains("\"updated_at\":\"2026-04-23T12:05:00Z\"", roundsJson);
        Assert.Contains("\"courses\":", previewJson);
        Assert.Contains("\"jx02id\":\"course-1001\"", previewJson);
        Assert.Contains("\"jx0404id\":\"section-2002\"", previewJson);
        Assert.Contains("\"grab_state\":", deferredJson);
        Assert.Contains("\"reason\":\"course-grab-deferred\"", deferredJson);
        Assert.DoesNotContain("\"StudentId\":", statusJson);
        Assert.DoesNotContain("\"GrabReason\":", statusJson);
    }

    [Fact]
    public void NetworkResponses_UseSnakeCaseForConfigStatusAndApplyResult()
    {
        var config = new NetworkConfigPayload(
            Settings: new NetworkSettingsDocument
            {
                Provider = "campus",
                HeartbeatEnabled = true,
                HeartbeatIntervalMinutes = 5,
                CampusIp = "10.0.0.8",
                CampusSubnetPrefix = 24,
                CampusGateway = "10.0.0.1",
                CampusDns = "223.5.5.5",
            },
            Credentials: new Dictionary<string, NetworkProviderCredential>
            {
                ["telecom"] = new("telecom-user", "te*******ss"),
            },
            ApplyResult: new NetworkApplyResult(
                Applied: false,
                Error: "requires admin",
                RequiresElevation: true,
                InterfaceName: "Ethernet 1",
                Ip: "10.0.0.8",
                SubnetMask: "255.255.255.0",
                Gateway: "10.0.0.1",
                Dns: "223.5.5.5",
                Interface: new Dictionary<string, object?> { ["name"] = "Ethernet 1" }),
            CampusAccount: "20260001",
            StorageDir: ".runtime/data/students/20260001");
        var status = new NetworkStatusPayload(
            CheckedAt: "2026-04-23T12:00:00Z",
            CampusConnected: true,
            InternetConnected: false,
            NeedsHeartbeat: true,
            PortalHost: "10.199.199.199",
            Portal: new Dictionary<string, object?> { ["online_user"] = "20260001" },
            Interface: new Dictionary<string, object?> { ["ipv4"] = "10.0.0.8" },
            External: new Dictionary<string, object?> { ["connected"] = false },
            Settings: config.Settings,
            Provider: "campus",
            ProviderLabel: "校园账号",
            Action: "heartbeat",
            Attempts: [new Dictionary<string, object?> { ["username"] = "20260001" }],
            Message: "校园网心跳已发送");

        var configJson = JsonSerializer.Serialize(config);
        var statusJson = JsonSerializer.Serialize(status);

        Assert.Contains("\"apply_result\":", configJson);
        Assert.Contains("\"requires_elevation\":true", configJson);
        Assert.Contains("\"campus_account\":\"20260001\"", configJson);
        Assert.Contains("\"storage_dir\":\".runtime/data/students/20260001\"", configJson);
        Assert.Contains("\"checked_at\":\"2026-04-23T12:00:00Z\"", statusJson);
        Assert.Contains("\"campus_connected\":true", statusJson);
        Assert.Contains("\"needs_heartbeat\":true", statusJson);
        Assert.Contains("\"provider_label\":", statusJson);
        Assert.Contains("\"attempts\":", statusJson);
    }

    [Fact]
    public void TelemetryAndFeedbackResponses_UseSnakeCase()
    {
        var telemetry = new TelemetryReportPayload(
            InstallUuid: "install-1",
            FirstUsedAt: "2026-04-23T12:00:00Z",
            ReportedAt: "2026-04-23T12:05:00Z",
            Cloud: new TelemetryCloudResult(
                Ok: true,
                Queued: false,
                Skipped: true,
                Reason: "local-only"),
            Encryption: new TelemetryEncryptionSummary(
                Algorithm: "AES-256-GCM",
                Fingerprint: "abc123",
                KeyPath: "client://xke-local-data-v1"),
            SnapshotPath: ".runtime/data/students/20260001/info/remote_sensing_snapshot.json");
        var feedback = new FeedbackReportPayload(
            FeedbackId: "feedback-1",
            StoredKey: ".runtime/data/.feedback/outbox/20260001/feedback-1.json",
            ReportedAt: "2026-04-23T12:06:00Z");

        var telemetryJson = JsonSerializer.Serialize(telemetry);
        var feedbackJson = JsonSerializer.Serialize(feedback);

        Assert.Contains("\"install_uuid\":\"install-1\"", telemetryJson);
        Assert.Contains("\"first_used_at\":\"2026-04-23T12:00:00Z\"", telemetryJson);
        Assert.Contains("\"snapshot_path\":", telemetryJson);
        Assert.Contains("\"key_path\":", telemetryJson);
        Assert.Contains("\"feedback_id\":\"feedback-1\"", feedbackJson);
        Assert.Contains("\"stored_key\":", feedbackJson);
        Assert.Contains("\"reported_at\":\"2026-04-23T12:06:00Z\"", feedbackJson);
    }
}
