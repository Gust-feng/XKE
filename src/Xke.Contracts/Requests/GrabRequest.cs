using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class GrabRequest
{
    [JsonPropertyName("zbid")]
    public required string Zbid { get; init; }

    [JsonPropertyName("kcid")]
    public required string Kcid { get; init; }

    [JsonPropertyName("jx0404id")]
    public required string Jx0404Id { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; init; } = "beta";

    [JsonPropertyName("build")]
    public string Build { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public double Interval { get; init; } = 0.5;
}
