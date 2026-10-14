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
            CampusAccount = "campus-user", CampusPassword = "campus-pass",
            TelecomAccount = "telecom-user",
            TelecomPassword = "telecom-pass",
        };

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"provider\":\"campus\"", json);
        Assert.Contains("\"campus_account\":\"campus-user\"", json);
        Assert.Contains("\"campus_password\":\"campus-pass\"", json);
        Assert.Contains("\"heartbeat_enabled\":true", json);
        Assert.Contains("\"heartbeat_interval_minutes\":15", json);
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
            TrustedLoginToken: "trusted-token");

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"session_id\":\"sid-1\"", json);
        Assert.Contains("\"student_id\":\"20260001\"", json);
        Assert.Contains("\"avatar_url\":\"/api/auth/avatar?v=1\"", json);
        Assert.Contains("\"updated_at\":\"2026-09-08T00:00:00Z\"", json);
        Assert.Contains("\"storage_dir\":\".runtime/data/students/20260001\"", json);
        Assert.Contains("\"prelogin_mode\":\"auto\"", json);
        Assert.Contains("\"ignored_weekend_blocks\":", json);
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
            FileUpdatedAt: "2026-04-23T12:00:01Z",
            SourceKey: "mobile_schedule");

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"source_label\":", json);
        Assert.Contains("\"source_type\":\"mobile\"", json);
        Assert.Contains("\"source_key\":\"mobile_schedule\"", json);
        Assert.Equal("mobile_schedule", JsonSerializer.Deserialize<ScheduleData>(json)!.SourceKey);
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
    public void PreferencesRequest_OmittedAndNullStringsAreOptionalWhileEmptyStringsAreExplicit()
    {
        var omitted = JsonSerializer.Deserialize<PreferencesRequest>("""
            {"schedule":{"auto_refresh_enabled":true},"network":{"heartbeat_enabled":true},"ui":{}}
            """)!;
        Assert.Null(omitted.Schedule!.AcademicTerm);
        Assert.Null(omitted.Schedule.ManualWeek);
        Assert.Null(omitted.Ui!.ShowWeekend);
        Assert.Null(omitted.Ui.InactiveCourseMode);
        var explicitValues = JsonSerializer.Deserialize<PreferencesRequest>("""
            {"schedule":{"academic_term":"","manual_week":null},"ui":{"show_weekend":false,"inactive_course_mode":"dim"}}
            """)!;
        Assert.Equal(string.Empty, explicitValues.Schedule!.AcademicTerm);
        Assert.Null(explicitValues.Schedule.ManualWeek);
        var json = JsonSerializer.Serialize(explicitValues.Ui);
        Assert.Contains("\"show_weekend\":false", json);
        Assert.Contains("\"inactive_course_mode\":\"dim\"", json);
        var defaults = JsonSerializer.Deserialize<AppSettingsDocument>("{\"ui\":{\"panels\":{}}}")!;
        Assert.True(defaults.Ui.ShowWeekend);
        Assert.Equal("dim", defaults.Ui.InactiveCourseMode);
    }

    [Fact]
    public void NetworkResponses_UseSnakeCaseForConfigAndStatus()
    {
        var config = new NetworkConfigPayload(
            Settings: new NetworkSettingsDocument
            {
                Provider = "campus",
                HeartbeatEnabled = true,
                HeartbeatIntervalMinutes = 5,
            },
            Credentials: new Dictionary<string, NetworkProviderCredential>
            {
                ["telecom"] = new("telecom-user", "te*******ss"),
            },
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
            Message: "校园网认证失败", OperationError: "服务器限制当前IP");

        var configJson = JsonSerializer.Serialize(config);
        var statusJson = JsonSerializer.Serialize(status);

        Assert.DoesNotContain("apply_result", configJson);

        Assert.Contains("\"campus_account\":\"20260001\"", configJson);
        Assert.Contains("\"storage_dir\":\".runtime/data/students/20260001\"", configJson);
        Assert.Contains("\"checked_at\":\"2026-04-23T12:00:00Z\"", statusJson);
        Assert.Contains("\"campus_connected\":true", statusJson);
        Assert.Contains("\"needs_heartbeat\":true", statusJson);
        Assert.Contains("\"provider_label\":", statusJson);
        Assert.Contains("\"operation_error\":", statusJson);
        Assert.Contains("\"attempts\":", statusJson);
    }

}
