using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public class NetworkSettingsRequest
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("heartbeat_enabled")]
    public bool? HeartbeatEnabled { get; init; }

    [JsonPropertyName("heartbeat_interval_minutes")]
    public int? HeartbeatIntervalMinutes { get; init; }

    [JsonPropertyName("campus_ip")]
    public string CampusIp { get; init; } = string.Empty;

    [JsonPropertyName("campus_subnet_prefix")]
    public int? CampusSubnetPrefix { get; init; }

    [JsonPropertyName("campus_gateway")]
    public string CampusGateway { get; init; } = string.Empty;

    [JsonPropertyName("campus_dns")]
    public string CampusDns { get; init; } = string.Empty;
}
