using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class PreviewRequest
{
    [JsonPropertyName("zbid")]
    public required string Zbid { get; init; }

    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; init; }
}
