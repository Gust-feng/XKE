using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record HostHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("sessions")] int Sessions,
    [property: JsonPropertyName("web_root_found")] bool WebRootFound,
    [property: JsonPropertyName("web_root")] string WebRoot,
    [property: JsonPropertyName("base_url")] string BaseUrl,
    [property: JsonPropertyName("frontend_build")] string FrontendBuild = "",
    [property: JsonPropertyName("static_asset_hashes")] IReadOnlyDictionary<string, string>? StaticAssetHashes = null,
    [property: JsonPropertyName("app_version")] string AppVersion = "",
    [property: JsonPropertyName("update_supported")] bool UpdateSupported = false
);

public sealed record HostDevelopmentResponse(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("host_mode")] string HostMode,
    [property: JsonPropertyName("base_url")] string BaseUrl,
    [property: JsonPropertyName("web_root")] string WebRoot,
    [property: JsonPropertyName("web_root_found")] bool WebRootFound
);

public sealed record HostConfigResponse(
    [property: JsonPropertyName("prelogin_mode")] string PreloginMode,
    [property: JsonPropertyName("development")] HostDevelopmentResponse Development,
    [property: JsonPropertyName("defaults")] Xke.Contracts.Models.AppSettingsDocument Defaults
);

public sealed record DesktopActivatePayload(
    [property: JsonPropertyName("activated")] bool Activated,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("window_state")] IReadOnlyDictionary<string, object?> WindowState
);
