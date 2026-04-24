using System.Text.Json.Serialization;
using Xke.Contracts.Models;

namespace Xke.Contracts.Responses;

public sealed record NetworkProviderCredential(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("password")] string Password);

public sealed record NetworkApplyResult(
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("requires_elevation")] bool RequiresElevation,
    [property: JsonPropertyName("interface_name")] string InterfaceName,
    [property: JsonPropertyName("ip")] string Ip,
    [property: JsonPropertyName("subnet_mask")] string SubnetMask,
    [property: JsonPropertyName("gateway")] string Gateway,
    [property: JsonPropertyName("dns")] string Dns,
    [property: JsonPropertyName("interface")] Dictionary<string, object?> Interface);

public sealed record NetworkConfigPayload(
    [property: JsonPropertyName("settings")] NetworkSettingsDocument Settings,
    [property: JsonPropertyName("credentials")] Dictionary<string, NetworkProviderCredential> Credentials,
    [property: JsonPropertyName("apply_result")] NetworkApplyResult? ApplyResult,
    [property: JsonPropertyName("campus_account")] string CampusAccount,
    [property: JsonPropertyName("storage_dir")] string StorageDir);

public sealed record NetworkStatusPayload(
    [property: JsonPropertyName("checked_at")] string CheckedAt,
    [property: JsonPropertyName("campus_connected")] bool CampusConnected,
    [property: JsonPropertyName("internet_connected")] bool InternetConnected,
    [property: JsonPropertyName("needs_heartbeat")] bool NeedsHeartbeat,
    [property: JsonPropertyName("portal_host")] string PortalHost,
    [property: JsonPropertyName("portal")] Dictionary<string, object?> Portal,
    [property: JsonPropertyName("interface")] Dictionary<string, object?> Interface,
    [property: JsonPropertyName("external")] Dictionary<string, object?> External,
    [property: JsonPropertyName("settings")] NetworkSettingsDocument Settings,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("provider_label")] string ProviderLabel,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("attempts")] IReadOnlyList<Dictionary<string, object?>> Attempts,
    [property: JsonPropertyName("message")] string Message);
