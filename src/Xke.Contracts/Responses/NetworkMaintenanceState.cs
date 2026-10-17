using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record NetworkMaintenanceState(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("next_run_at")] DateTimeOffset? NextRunAt,
    [property: JsonPropertyName("last_run_at")] DateTimeOffset? LastRunAt,
    [property: JsonPropertyName("last_message")] string LastMessage,
    [property: JsonPropertyName("last_error")] string LastError);
