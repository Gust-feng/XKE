// Account-scoped network credentials, independent of the teaching-system login.
using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class NetworkConfigRequest : NetworkSettingsRequest
{
    [JsonPropertyName("campus_account")]
    public string CampusAccount { get; init; } = string.Empty;

    [JsonPropertyName("campus_password")]
    public string CampusPassword { get; init; } = string.Empty;

    [JsonPropertyName("telecom_account")]
    public string TelecomAccount { get; init; } = string.Empty;

    [JsonPropertyName("telecom_password")]
    public string TelecomPassword { get; init; } = string.Empty;

    [JsonPropertyName("unicom_account")]
    public string UnicomAccount { get; init; } = string.Empty;

    [JsonPropertyName("unicom_password")]
    public string UnicomPassword { get; init; } = string.Empty;

    [JsonPropertyName("mobile_account")]
    public string MobileAccount { get; init; } = string.Empty;

    [JsonPropertyName("mobile_password")]
    public string MobilePassword { get; init; } = string.Empty;
}
