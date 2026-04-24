using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class ScheduleRequest
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "current";

    [JsonPropertyName("manual_week")]
    public string ManualWeek { get; init; } = string.Empty;

    [JsonPropertyName("cookie_rq")]
    public string? CookieRq { get; init; }

    [JsonPropertyName("term_label")]
    public string TermLabel { get; init; } = string.Empty;

    [JsonPropertyName("academic_term")]
    public string AcademicTerm { get; init; } = string.Empty;
}
