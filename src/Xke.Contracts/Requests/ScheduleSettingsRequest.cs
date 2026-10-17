namespace Xke.Contracts.Requests;

public sealed class ScheduleSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("source")]
    public string? Source { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("developer_mode")]
    public bool? DeveloperMode { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("week_mode")]
    public string? WeekMode { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("manual_week")]
    public string? ManualWeek { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("cookie_rq")]
    public string? CookieRq { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("term_label")]
    public string? TermLabel { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("academic_term")]
    public string? AcademicTerm { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("mock_day")]
    public string? MockDay { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("mock_time")]
    public string? MockTime { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("auto_refresh_enabled")]
    public bool? AutoRefreshEnabled { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("auto_refresh_mode")]
    public string? AutoRefreshMode { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("auto_refresh_daily_time")]
    public string? AutoRefreshDailyTime { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("auto_refresh_interval_hours")]
    public int? AutoRefreshIntervalHours { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("lesson_reminder_enabled")]
    public bool? LessonReminderEnabled { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("lesson_reminder_minutes")]
    public int? LessonReminderMinutes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("weekend_auto_switch_enabled")]
    public bool? WeekendAutoSwitchEnabled { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("ignored_weekend_blocks")]
    public Dictionary<string, List<int>>? IgnoredWeekendBlocks { get; init; }
}
