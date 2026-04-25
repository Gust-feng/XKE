using System.Text.Json.Serialization;
using Xke.Contracts.Enums;

namespace Xke.Contracts.Requests;

public sealed class TrustedLoginRequest
{
    [JsonPropertyName("user")]
    public required string User { get; init; }

    [JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = AppOptions.DefaultPreloginMode;

    [JsonPropertyName("trusted_login_token")]
    public string TrustedLoginToken { get; init; } = string.Empty;
}
