using System.Security.Cryptography;
using System.Text.Json;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeTelemetryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;

    public XkeTelemetryStore(XkeDataRoot root)
    {
        _root = root;
    }

    public Dictionary<string, object?> LoadOrCreateInstallationProfile(Func<string>? timestampFactory = null)
    {
        var existing = ReadJson(_root.TelemetryInstallationPath);
        if (!string.IsNullOrWhiteSpace(existing.GetValueOrDefault("install_uuid")?.ToString()))
        {
            return existing;
        }

        var createdAt = ResolveTimestamp(timestampFactory);
        var payload = new Dictionary<string, object?>
        {
            ["install_uuid"] = Guid.NewGuid().ToString("N"),
            ["first_used_at"] = createdAt,
            ["created_at"] = createdAt,
            ["last_reported_at"] = string.Empty,
        };
        WriteJson(_root.TelemetryInstallationPath, payload);
        return payload;
    }

    public string SaveInstallationProfile(Dictionary<string, object?> payload)
    {
        WriteJson(_root.TelemetryInstallationPath, payload);
        return _root.RelativePath(_root.TelemetryInstallationPath);
    }

    public TelemetryKeyProfile LoadOrCreateKeyProfile(Func<string>? timestampFactory = null)
    {
        if (!File.Exists(_root.TelemetryKeyPath))
        {
            // This AES key only describes the local telemetry profile and never leaves the device in this slice.
            var payload = new Dictionary<string, object?>
            {
                ["algorithm"] = "AES-256-GCM",
                ["created_at"] = ResolveTimestamp(timestampFactory),
                ["key_b64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            };
            WriteJson(_root.TelemetryKeyPath, payload);
        }

        return TelemetryKeyProfile.LoadFromDataRoot(_root.RootPath);
    }

    public string EnsureSpoolDirectory()
    {
        Directory.CreateDirectory(_root.TelemetrySpoolDirectory);
        return _root.RelativePath(_root.TelemetrySpoolDirectory);
    }

    private static string ResolveTimestamp(Func<string>? timestampFactory)
    {
        var value = timestampFactory?.Invoke()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? DateTimeOffset.Now.ToString("O") : value;
    }

    private static Dictionary<string, object?> ReadJson(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
    }

    private static void WriteJson(string path, Dictionary<string, object?> payload)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }
}
