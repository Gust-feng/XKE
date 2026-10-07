using System.Text.Json;

namespace Xke.Core.CloudSync;

public sealed class PrivateCloudEndpointOptions
{
    public const string ConfigPathEnvironmentVariable = "XKE_PRIVATE_CLOUD_CONFIG";
    public const string RuntimeConfigFileName = "xke.runtime.json";

    private static readonly string[] AutoConfigRelativePaths =
    [
        "开发数据/dotnet8-cloud.local.json",
        "开发数据/cloud-endpoints.local.json",
        "开发数据/云端配置.local.json",
    ];

    public string NoticeBaseUrl { get; init; } = string.Empty;
    public string TelemetryCloudEndpoint { get; init; } = string.Empty;
    public string FeedbackCloudEndpoint { get; init; } = string.Empty;
    public bool LocalOnly { get; init; }

    public static PrivateCloudEndpointOptions Load()
    {
        var path = ResolveConfigPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new PrivateCloudEndpointOptions();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new PrivateCloudEndpointOptions
            {
                NoticeBaseUrl = FirstNonEmpty(
                    ReadNestedString(root, "remote", "n"),
                    ReadNestedString(root, "remote", "notice"),
                    ReadString(root, "notice_base_url"),
                    ReadString(root, "cloud_notice_base_url"),
                    ReadString(root, "cos_base_url"),
                    ReadString(root, "cloud_feed_base_url"),
                    ReadNestedString(root, "notice", "base_url"),
                    ReadNestedString(root, "notice", "cos_base_url")),
                TelemetryCloudEndpoint = FirstNonEmpty(
                    ReadNestedString(root, "remote", "t"),
                    ReadNestedString(root, "remote", "report"),
                    ReadString(root, "telemetry_cloud_endpoint"),
                    ReadString(root, "telemetry_base_url"),
                    ReadString(root, "cloud_function_base_url"),
                    ReadNestedString(root, "telemetry", "endpoint"),
                    ReadNestedString(root, "telemetry", "base_url"),
                    ReadNestedString(root, "cloud_function", "base_url")),
                FeedbackCloudEndpoint = FirstNonEmpty(
                    ReadNestedString(root, "remote", "f"),
                    ReadNestedString(root, "remote", "feedback"),
                    ReadString(root, "feedback_cloud_endpoint"),
                    ReadString(root, "feedback_base_url"),
                    ReadString(root, "cloud_feedback_base_url"),
                    ReadNestedString(root, "feedback", "endpoint"),
                    ReadNestedString(root, "feedback", "base_url"),
                    ReadNestedString(root, "cloud_function", "feedback_base_url")),
                LocalOnly = ReadBoolean(root, "local_only"),
            };
        }
        catch (JsonException ex) when (IsAutoDiscoveredConfig(path))
        {
            throw new InvalidOperationException($"Private cloud endpoint config is not valid JSON: {path}", ex);
        }
    }

    private static string ResolveConfigPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath.Trim());
        }

        var runtimeConfigPath = Path.Combine(AppContext.BaseDirectory, RuntimeConfigFileName);
        if (File.Exists(runtimeConfigPath))
        {
            return runtimeConfigPath;
        }

        var workspaceRoot = ResolveWorkspaceRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return string.Empty;
        }

        foreach (var relativePath in AutoConfigRelativePaths)
        {
            var candidate = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static bool IsAutoDiscoveredConfig(string path)
    {
        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable));
    }

    private static string ResolveWorkspaceRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XKE.DotNet8.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return string.Empty;
    }

    private static string ReadNestedString(JsonElement root, string objectName, string propertyName)
    {
        return root.TryGetProperty(objectName, out var child) && child.ValueKind == JsonValueKind.Object
            ? ReadString(child, propertyName)
            : string.Empty;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.True
               || (property.ValueKind == JsonValueKind.String
                   && bool.TryParse(property.GetString(), out var parsed)
                   && parsed);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
