using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class ConnectRequest
{
    [JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = string.Empty;
}
