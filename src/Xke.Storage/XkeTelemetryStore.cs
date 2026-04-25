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
    private readonly LocalEncryptedFileStore _encrypted = new();

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
        return TelemetryKeyProfile.CreateClientLocal();
    }

    public string EnsureSpoolDirectory()
    {
        Directory.CreateDirectory(_root.TelemetrySpoolDirectory);
        return _root.RelativePath(_root.TelemetrySpoolDirectory);
    }

    public string SaveSpoolItem(Dictionary<string, object?> payload, string queuedAt)
    {
        return _root.CloudSync().SaveTelemetrySpoolItem(payload, queuedAt);
    }

    public bool DeleteSpoolItem(string relativePath)
    {
        return _root.CloudSync().DeleteTelemetrySpoolItem(relativePath);
    }

    public IReadOnlyList<string> EnumerateSpoolItems(int maxItems)
    {
        return _root.CloudSync().EnumerateTelemetrySpoolItems(maxItems);
    }

    public Dictionary<string, object?> LoadSpoolItem(string relativePath)
    {
        return _root.CloudSync().LoadTelemetrySpoolItem(relativePath);
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

        using var document = JsonDocument.Parse(new LocalEncryptedFileStore().ReadBytes(path));
        return LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
    }

    private void WriteJson(string path, Dictionary<string, object?> payload)
    {
        _encrypted.WriteJson(path, payload);
    }
}
