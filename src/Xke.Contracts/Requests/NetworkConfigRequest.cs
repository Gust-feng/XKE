// 本文件对应 Python `api/routes/network.py` 中的 `NetworkConfigRequest`。
// 职责：在基础网络设置之外，承载三家运营商账号与密码字段，
// 作为 WebHost -> NetworkKeepAlive 域的配置输入契约。
using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class NetworkConfigRequest : NetworkSettingsRequest
{
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
