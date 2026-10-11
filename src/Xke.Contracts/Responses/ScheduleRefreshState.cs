using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record ScheduleRefreshState(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("next_run_at")] DateTimeOffset? NextRunAt,
    [property: JsonPropertyName("last_run_at")] DateTimeOffset? LastRunAt,
    [property: JsonPropertyName("requires_login")] bool RequiresLogin,
    [property: JsonPropertyName("last_error")] string LastError);
