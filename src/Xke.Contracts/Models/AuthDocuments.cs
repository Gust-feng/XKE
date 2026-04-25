using System.Text.Json.Serialization;
using Xke.Contracts.Enums;

namespace Xke.Contracts.Models;

public sealed class AppSettingsDocument
{
    [JsonPropertyName("appearance")]
    public AppearanceSettingsDocument Appearance { get; init; } = new();

    [JsonPropertyName("schedule")]
    public ScheduleSettingsDocument Schedule { get; init; } = new();

    [JsonPropertyName("auth")]
    public AuthSettingsDocument Auth { get; init; } = new();

    [JsonPropertyName("network")]
    public NetworkSettingsDocument Network { get; init; } = new();

    [JsonPropertyName("ui")]
    public UiSettingsDocument Ui { get; init; } = new();
}

public sealed class AppearanceSettingsDocument
{
    [JsonPropertyName("theme")]
    public string Theme { get; init; } = string.Empty;
}

public sealed class ScheduleSettingsDocument
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = "preferred";

    [JsonPropertyName("developer_mode")]
    public bool DeveloperMode { get; init; }

    [JsonPropertyName("week_mode")]
    public string WeekMode { get; init; } = "current";

    [JsonPropertyName("manual_week")]
    public string ManualWeek { get; init; } = string.Empty;

    [JsonPropertyName("cookie_rq")]
    public string CookieRq { get; init; } = string.Empty;

    [JsonPropertyName("term_label")]
    public string TermLabel { get; init; } = string.Empty;

    [JsonPropertyName("academic_term")]
    public string AcademicTerm { get; init; } = string.Empty;

    [JsonPropertyName("mock_day")]
    public string MockDay { get; init; } = string.Empty;

    [JsonPropertyName("mock_time")]
    public string MockTime { get; init; } = string.Empty;

    [JsonPropertyName("auto_refresh_enabled")]
    public bool AutoRefreshEnabled { get; init; }

    [JsonPropertyName("auto_refresh_mode")]
    public string AutoRefreshMode { get; init; } = "daily";

    [JsonPropertyName("auto_refresh_daily_time")]
    public string AutoRefreshDailyTime { get; init; } = "20:00";

    [JsonPropertyName("auto_refresh_interval_hours")]
    public int AutoRefreshIntervalHours { get; init; } = 4;

    [JsonPropertyName("lesson_reminder_enabled")]
    public bool LessonReminderEnabled { get; init; }

    [JsonPropertyName("lesson_reminder_minutes")]
    public int LessonReminderMinutes { get; init; } = 15;

    [JsonPropertyName("weekend_auto_switch_enabled")]
    public bool WeekendAutoSwitchEnabled { get; init; }

    [JsonPropertyName("ignored_weekend_blocks")]
    public IgnoredWeekendBlocksDocument IgnoredWeekendBlocks { get; init; } = new();
}

public sealed class IgnoredWeekendBlocksDocument
{
    [JsonPropertyName("saturday")]
    public IReadOnlyList<int> Saturday { get; init; } = Array.Empty<int>();

    [JsonPropertyName("sunday")]
    public IReadOnlyList<int> Sunday { get; init; } = Array.Empty<int>();
}

public sealed class AuthSettingsDocument
{
    [JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = AppOptions.DefaultPreloginMode;
}

public sealed class NetworkSettingsDocument
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "campus";

    [JsonPropertyName("heartbeat_enabled")]
    public bool HeartbeatEnabled { get; init; }

    [JsonPropertyName("heartbeat_interval_minutes")]
    public int HeartbeatIntervalMinutes { get; init; } = 5;

    [JsonPropertyName("campus_ip")]
    public string CampusIp { get; init; } = string.Empty;

    [JsonPropertyName("campus_subnet_prefix")]
    public int CampusSubnetPrefix { get; init; } = 24;

    [JsonPropertyName("campus_gateway")]
    public string CampusGateway { get; init; } = string.Empty;

    [JsonPropertyName("campus_dns")]
    public string CampusDns { get; init; } = string.Empty;
}

public sealed class UiSettingsDocument
{
    [JsonPropertyName("panels")]
    public UiPanelsDocument Panels { get; init; } = new();
}

public sealed class UiPanelsDocument
{
    [JsonPropertyName("schedule")]
    public bool Schedule { get; init; }

    [JsonPropertyName("login_mode")]
    public bool LoginMode { get; init; }

    [JsonPropertyName("appearance")]
    public bool Appearance { get; init; }
}

public sealed class DesktopSettingsDocument
{
    [JsonPropertyName("launch_at_startup")]
    public bool LaunchAtStartup { get; init; }

    [JsonPropertyName("silent_startup")]
    public bool SilentStartup { get; init; }

    [JsonPropertyName("close_to_tray")]
    public bool CloseToTray { get; init; } = true;
}

public sealed class LegacyPreferencesDocument
{
    [JsonPropertyName("theme")]
    public string Theme { get; init; } = string.Empty;
}

public sealed class StudentProfileDocument
{
    [JsonPropertyName("student_id")]
    public string StudentId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("nickname")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("use_nickname")]
    public bool UseNickname { get; init; }

    [JsonPropertyName("class_name")]
    public string ClassName { get; init; } = string.Empty;

    [JsonPropertyName("major_name")]
    public string MajorName { get; init; } = string.Empty;

    [JsonPropertyName("academy_name")]
    public string AcademyName { get; init; } = string.Empty;

    [JsonPropertyName("training_level")]
    public string TrainingLevel { get; init; } = string.Empty;

    [JsonPropertyName("campus_name")]
    public string CampusName { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class StoredCredentialDocument
{
    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("student_id")]
    public string StudentId { get; init; } = string.Empty;

    [JsonPropertyName("trusted_login_token")]
    public string TrustedLoginToken { get; init; } = string.Empty;

    [JsonPropertyName("mobile_jw")]
    public MobileJwEncryptedCredentialDocument? MobileJw { get; init; }

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class MobileJwEncryptedCredentialDocument
{
    [JsonPropertyName("encrypted_password")]
    public string EncryptedPassword { get; init; } = string.Empty;

    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; init; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; init; } = string.Empty;
}

public sealed class StoredSessionDocument
{
    [JsonPropertyName("session_id_hash")]
    public string SessionIdHash { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;

    [JsonPropertyName("student_id")]
    public string StudentId { get; init; } = string.Empty;

    [JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = AppOptions.DefaultPreloginMode;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class StoredNetworkCredentialDocument
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("account")]
    public string Account { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class NetworkAuthHistoryDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "2026-04-23";

    [JsonPropertyName("records")]
    public IReadOnlyList<NetworkAuthRecordDocument> Records { get; init; } = Array.Empty<NetworkAuthRecordDocument>();
}

public sealed class NetworkAuthRecordDocument
{
    [JsonPropertyName("recorded_at")]
    public string RecordedAt { get; init; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("provider_label")]
    public string ProviderLabel { get; init; } = string.Empty;

    [JsonPropertyName("account")]
    public string Account { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public Dictionary<string, object?> Status { get; init; } = [];

    [JsonPropertyName("attempts")]
    public IReadOnlyList<Dictionary<string, object?>> Attempts { get; init; } = Array.Empty<Dictionary<string, object?>>();
}
