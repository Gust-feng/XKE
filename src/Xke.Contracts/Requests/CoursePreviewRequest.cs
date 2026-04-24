using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class CoursePreviewRequest
{
    [JsonPropertyName("round_id")]
    public required string RoundId { get; init; }

    [JsonPropertyName("refresh")]
    public bool Refresh { get; init; }
}
