namespace Xke.Contracts.Requests;

public sealed class AppearanceSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("theme")]
    public string Theme { get; init; } = string.Empty;
}
