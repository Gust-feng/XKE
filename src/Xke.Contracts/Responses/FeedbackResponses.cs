using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record FeedbackReportPayload(
    [property: JsonPropertyName("feedback_id")] string FeedbackId,
    [property: JsonPropertyName("stored_key")] string StoredKey,
    [property: JsonPropertyName("reported_at")] string ReportedAt);
