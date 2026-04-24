using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class PreferencesRequest
{
    [JsonPropertyName("theme")]
    public string Theme { get; init; } = string.Empty;

    [JsonPropertyName("appearance")]
    public AppearanceSettingsRequest? Appearance { get; init; }

    [JsonPropertyName("schedule")]
    public ScheduleSettingsRequest? Schedule { get; init; }

    [JsonPropertyName("network")]
    public NetworkSettingsRequest? Network { get; init; }

    [JsonPropertyName("auth")]
    public AuthSettingsRequest? Auth { get; init; }

    [JsonPropertyName("ui")]
    public UISettingsRequest? Ui { get; init; }
}
