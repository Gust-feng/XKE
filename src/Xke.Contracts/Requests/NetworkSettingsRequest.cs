using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public class NetworkSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("portal_url")]
    public string? PortalUrl { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("connectivity_check_urls")]
    public IReadOnlyList<string>? ConnectivityCheckUrls { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("heartbeat_enabled")]
    public bool? HeartbeatEnabled { get; init; }

    [JsonPropertyName("heartbeat_interval_minutes")]
    public int? HeartbeatIntervalMinutes { get; init; }

}
