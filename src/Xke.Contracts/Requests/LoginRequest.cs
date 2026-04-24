using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class LoginRequest
{
    [JsonPropertyName("user")]
    public required string User { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = "capture";
}
