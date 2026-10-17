using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record CampusSelfServiceSummary
{
    [JsonPropertyName("configured")] public bool Configured { get; init; }
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("cached")] public bool Cached { get; init; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("account_status")] public string? AccountStatus { get; init; }
    [JsonPropertyName("plan_name")] public string? PlanName { get; init; }
    [JsonPropertyName("billing_method")] public string? BillingMethod { get; init; }
    [JsonPropertyName("period_start")] public string? PeriodStart { get; init; }
    [JsonPropertyName("period_end")] public string? PeriodEnd { get; init; }
}
