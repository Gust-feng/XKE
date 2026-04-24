using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record HostHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("sessions")] int Sessions,
    [property: JsonPropertyName("web_root_found")] bool WebRootFound,
    [property: JsonPropertyName("web_root")] string WebRoot,
    [property: JsonPropertyName("base_url")] string BaseUrl,
    [property: JsonPropertyName("frontend_build")] string FrontendBuild = "",
    [property: JsonPropertyName("static_asset_hashes")] IReadOnlyDictionary<string, string>? StaticAssetHashes = null
);

public sealed record HostBetaResponse(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("message")] string Message
);

public sealed record HostDevelopmentResponse(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("host_mode")] string HostMode,
    [property: JsonPropertyName("base_url")] string BaseUrl,
    [property: JsonPropertyName("web_root")] string WebRoot,
    [property: JsonPropertyName("web_root_found")] bool WebRootFound
);

public sealed record HostRemoteResponse(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("config")] IReadOnlyDictionary<string, object?> Config
);

public sealed record HostConfigResponse(
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("zbid")] string Zbid,
    [property: JsonPropertyName("prelogin_mode")] string PreloginMode,
    [property: JsonPropertyName("beta")] HostBetaResponse Beta,
    [property: JsonPropertyName("development")] HostDevelopmentResponse Development,
    [property: JsonPropertyName("remote")] HostRemoteResponse Remote
);

public sealed record DesktopActivatePayload(
    [property: JsonPropertyName("activated")] bool Activated,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("window_state")] IReadOnlyDictionary<string, object?> WindowState
);
