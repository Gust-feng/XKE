namespace Xke.Contracts.Requests;

public sealed class AuthSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("auto_captcha_enabled")]
    public bool? AutoCaptchaEnabled { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("prelogin_mode")]
    public string PreloginMode { get; init; } = string.Empty;
}
