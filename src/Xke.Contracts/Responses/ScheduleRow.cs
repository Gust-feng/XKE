namespace Xke.Contracts.Responses;

public sealed class ScheduleRow
{
    [System.Text.Json.Serialization.JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("week_label")]
    public string WeekLabel { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("weekday")]
    public string Weekday { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("section")]
    public string Section { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("time_text")]
    public string TimeText { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("time_range")]
    public string TimeRange { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("block_time")]
    public string BlockTime { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("small_section_1")]
    public string SmallSection1 { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("small_section_2")]
    public string SmallSection2 { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("break_minutes")]
    public int BreakMinutes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("course_name")]
    public string CourseName { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("teacher")]
    public string Teacher { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("room")]
    public string Room { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("weeks")]
    public string Weeks { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("credit")]
    public string Credit { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("section_numbers")]
    public IReadOnlyList<int> SectionNumbers { get; init; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("week_numbers")]
    public IReadOnlyList<int> WeekNumbers { get; init; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("time_segments")]
    public IReadOnlyList<string> TimeSegments { get; init; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("hours_text")]
    public string HoursText { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("hours_variants")]
    public IReadOnlyList<string> HoursVariants { get; init; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("conflict")]
    public ScheduleConflict? Conflict { get; init; }
}
