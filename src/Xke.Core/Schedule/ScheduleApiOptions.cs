using System.Text.Json;
using System.Text.Json.Serialization;
using Xke.Security;

namespace Xke.Core.Schedule;

public sealed class ScheduleApiOptions
{
    public const string ConfigEnvironmentVariable = "XKE_SCHEDULE_API_CONFIG";
    public const string RuntimeFileName = "xke.schedule-api.json";

    [JsonIgnore] public bool InvalidConfiguration { get; init; }

    [JsonPropertyName("provider")] public string Provider { get; init; } = "snapshot";
    [JsonPropertyName("endpoint")] public string Endpoint { get; init; } = string.Empty;
    [JsonPropertyName("certificate_sha256")] public string CertificateSha256 { get; init; } = string.Empty;
    [JsonPropertyName("protected_token_file")] public string ProtectedTokenFile { get; init; } = string.Empty;
    [JsonIgnore] public string ApiKey { get; private set; } = string.Empty;

    [JsonPropertyName("bearer_token")] public string BearerToken { get; init; } = string.Empty;
    [JsonPropertyName("student_id")] public string StudentId { get; init; } = string.Empty;
    [JsonPropertyName("academic_term")] public string AcademicTerm { get; init; } = string.Empty;
    [JsonPropertyName("timeout_seconds")] public int TimeoutSeconds { get; init; } = 8;
    [JsonPropertyName("max_age_hours")] public int MaxAgeHours { get; init; } = 36;

    public static ScheduleApiOptions Load(string workspaceRoot)
    {
        var explicitPath = Environment.GetEnvironmentVariable(ConfigEnvironmentVariable);
        var file = !string.IsNullOrWhiteSpace(explicitPath)
            ? Path.GetFullPath(explicitPath)
            : Path.Combine(AppContext.BaseDirectory, RuntimeFileName);
        if (string.IsNullOrWhiteSpace(explicitPath) && !File.Exists(file))
            file = Path.Combine(workspaceRoot, "开发数据", "课表服务", "client.local.json");
        if (!File.Exists(file)) return new() { InvalidConfiguration = !string.IsNullOrWhiteSpace(explicitPath) };
        try
        {
            var options = JsonSerializer.Deserialize<ScheduleApiOptions>(File.ReadAllText(file));
            if (options is null || options.Provider is not ("snapshot" or "protocol")
                || options.TimeoutSeconds is < 1 or > 60 || options.MaxAgeHours is < 1 or > 720)
                return new() { InvalidConfiguration = true };
            if (!string.IsNullOrWhiteSpace(options.ProtectedTokenFile))
            {
                var tokenPath = Path.GetFullPath(options.ProtectedTokenFile, Path.GetDirectoryName(Path.GetFullPath(file))!);
                options.ApiKey = new ProtectedJsonFileStore().Read<string>(tokenPath)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(options.ApiKey)) return new() { InvalidConfiguration = true };
            }
            if (!string.IsNullOrWhiteSpace(options.CertificateSha256)
                && !ScheduleApiTransport.IsValidFingerprint(options.CertificateSha256))
                return new() { InvalidConfiguration = true };
            return options;
        }
        catch (JsonException) { return new() { InvalidConfiguration = true }; }
        catch (IOException) { return new() { InvalidConfiguration = true }; }
        catch (UnauthorizedAccessException) { return new() { InvalidConfiguration = true }; }
        catch (InvalidOperationException) { return new() { InvalidConfiguration = true }; }
        catch (FormatException) { return new() { InvalidConfiguration = true }; }
    }
}
