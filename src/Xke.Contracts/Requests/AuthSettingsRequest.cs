namespace Xke.Contracts.Requests;

public sealed class AuthSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = string.Empty;
}
