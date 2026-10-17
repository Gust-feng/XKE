using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xke.Core.Network;

public sealed class CampusSelfServiceOptions
{
    public const string ConfigEnvironmentVariable = "XKE_CAMPUS_SELF_SERVICE_CONFIG";
    public const string RuntimeFileName = "xke.campus-self-service.json";

    [JsonPropertyName("endpoint")] public string Endpoint { get; init; } = string.Empty;
    [JsonPropertyName("timeout_seconds")] public int TimeoutSeconds { get; init; } = 8;
    [JsonIgnore] public bool InvalidConfiguration { get; init; }
    [JsonIgnore] public bool Configured => !InvalidConfiguration
        && TimeoutSeconds is >= 1 and <= 60
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.AbsolutePath.TrimEnd('/') == "/Self"
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;

    public static CampusSelfServiceOptions Load(string workspaceRoot)
    {
        var explicitPath = Environment.GetEnvironmentVariable(ConfigEnvironmentVariable);
        var path = !string.IsNullOrWhiteSpace(explicitPath)
            ? explicitPath
            : Path.Combine(AppContext.BaseDirectory, RuntimeFileName);
        if (string.IsNullOrWhiteSpace(explicitPath) && !File.Exists(path))
            path = Path.Combine(workspaceRoot, "开发数据", "校园网", "self-service.local.json");
        if (!File.Exists(path)) return new() { InvalidConfiguration = !string.IsNullOrWhiteSpace(explicitPath) };
        try
        {
            return JsonSerializer.Deserialize<CampusSelfServiceOptions>(File.ReadAllText(path))
                ?? new() { InvalidConfiguration = true };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new() { InvalidConfiguration = true };
        }
    }
}
