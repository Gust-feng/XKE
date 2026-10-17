using System.Text.Json.Serialization;
using Xke.Contracts.Models;

namespace Xke.Contracts.Responses;

public sealed record DesktopSettingsPayload(
    [property: JsonPropertyName("settings")] DesktopSettingsDocument Settings,
    [property: JsonPropertyName("startup_registered")] bool? StartupRegistered
);

public sealed record DesktopAuthBootstrapPayload(
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("auth")] AuthPayload Auth
);
