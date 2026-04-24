namespace Xke.Contracts.Requests;

public sealed class UIPanelSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("schedule")]
    public bool? Schedule { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("login_mode")]
    public bool? LoginMode { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("appearance")]
    public bool? Appearance { get; init; }
}

public sealed class UISettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("panels")]
    public UIPanelSettingsRequest? Panels { get; init; }
}
