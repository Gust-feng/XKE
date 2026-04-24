using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class CourseGrabStartRequest
{
    [JsonPropertyName("round_id")]
    public string RoundId { get; init; } = string.Empty;

    [JsonPropertyName("course_id")]
    public string CourseId { get; init; } = string.Empty;

    [JsonPropertyName("section_id")]
    public string SectionId { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public double Interval { get; init; } = 0.5;
}
