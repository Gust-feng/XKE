using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class DesktopSettingsRequest
{
    [JsonPropertyName("auto_update_enabled")]
    public bool? AutoUpdateEnabled { get; init; }

    [JsonPropertyName("launch_at_startup")]
    public bool? LaunchAtStartup { get; init; }

    [JsonPropertyName("silent_startup")]
    public bool? SilentStartup { get; init; }

    [JsonPropertyName("close_to_tray")]
    public bool? CloseToTray { get; init; }
}
