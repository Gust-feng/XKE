using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class FeedbackRequest
{
    [JsonPropertyName("category")]
    public string Category { get; init; } = "other";

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("contact")]
    public string Contact { get; init; } = string.Empty;

    [JsonPropertyName("build")]
    public string Build { get; init; } = string.Empty;

    [JsonPropertyName("current_tab")]
    public string CurrentTab { get; init; } = string.Empty;
}
