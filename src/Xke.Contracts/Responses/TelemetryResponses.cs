using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record TelemetryCloudResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("queued")] bool Queued,
    [property: JsonPropertyName("skipped")] bool Skipped,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record TelemetryEncryptionSummary(
    [property: JsonPropertyName("algorithm")] string Algorithm,
    [property: JsonPropertyName("fingerprint")] string Fingerprint,
    [property: JsonPropertyName("key_path")] string KeyPath);

public sealed record TelemetryReportPayload(
    [property: JsonPropertyName("install_uuid")] string InstallUuid,
    [property: JsonPropertyName("first_used_at")] string FirstUsedAt,
    [property: JsonPropertyName("reported_at")] string ReportedAt,
    [property: JsonPropertyName("cloud")] TelemetryCloudResult Cloud,
    [property: JsonPropertyName("encryption")] TelemetryEncryptionSummary Encryption,
    [property: JsonPropertyName("snapshot_path")] string SnapshotPath);
