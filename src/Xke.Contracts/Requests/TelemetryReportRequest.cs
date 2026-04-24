using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class TelemetryReportRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "heartbeat";

    [JsonPropertyName("session_started_at")]
    public string SessionStartedAt { get; init; } = string.Empty;

    [JsonPropertyName("foreground_seconds_delta")]
    public int ForegroundSecondsDelta { get; init; }

    [JsonPropertyName("background_seconds_delta")]
    public int BackgroundSecondsDelta { get; init; }

    [JsonPropertyName("active_tab")]
    public string ActiveTab { get; init; } = string.Empty;

    [JsonPropertyName("module_hint")]
    public string ModuleHint { get; init; } = string.Empty;

    [JsonPropertyName("build")]
    public string Build { get; init; } = string.Empty;

    [JsonPropertyName("student_id")]
    public string StudentId { get; init; } = string.Empty;

    [JsonPropertyName("feature_usage_deltas")]
    public Dictionary<string, object> FeatureUsageDeltas { get; init; } = [];

    [JsonPropertyName("cloud_upload_enabled")]
    public bool CloudUploadEnabled { get; init; } = true;

    [JsonPropertyName("cloud_skip_reason")]
    public string CloudSkipReason { get; init; } = string.Empty;
}
