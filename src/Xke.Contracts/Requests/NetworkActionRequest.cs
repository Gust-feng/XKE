using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class NetworkActionRequest
{
    [JsonPropertyName("force")]
    public bool Force { get; init; }
}
