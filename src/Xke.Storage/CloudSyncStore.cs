using System.Text.Json;
using Xke.Security;

namespace Xke.Storage;

public sealed class CloudSyncStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;
    private readonly LocalEncryptedFileStore _encrypted = new();

    public CloudSyncStore(XkeDataRoot root)
    {
        _root = root;
    }

    public Dictionary<string, object?> LoadFeed()
    {
        return ReadJson(_root.CloudFeedPath);
    }

    public void SaveFeed(Dictionary<string, object?> payload)
    {
        WriteJson(_root.CloudFeedPath, payload);
    }

    public CloudFeedCacheMeta LoadFeedMeta()
    {
        if (!File.Exists(_root.CloudFeedMetaPath))
        {
            return new CloudFeedCacheMeta();
        }

        try
        {
            return _encrypted.ReadJson<CloudFeedCacheMeta>(_root.CloudFeedMetaPath) ?? new CloudFeedCacheMeta();
        }
        catch
        {
            return new CloudFeedCacheMeta();
        }
    }

    public void SaveFeedMeta(CloudFeedCacheMeta meta)
    {
        WriteJson(_root.CloudFeedMetaPath, meta);
    }

    public Dictionary<string, object?> LoadNoticeContentMeta()
    {
        return ReadJson(_root.CloudNoticeContentMetaPath);
    }

    public void SaveNoticeContentMeta(Dictionary<string, object?> payload)
    {
        WriteJson(_root.CloudNoticeContentMetaPath, payload);
    }

    public bool TryLoadNoticeContent(string cacheFileName, out string content)
    {
        content = string.Empty;
        var path = ResolveNoticeContentPath(cacheFileName);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            content = _encrypted.ReadText(path);
            return true;
        }
        catch
        {
            content = string.Empty;
            return false;
        }
    }

    public void SaveNoticeContent(string cacheFileName, string content)
    {
        var path = ResolveNoticeContentPath(cacheFileName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _encrypted.WriteText(path, content ?? string.Empty);
    }

    public string SaveTelemetrySpoolItem(Dictionary<string, object?> payload, string queuedAt)
    {
        var day = ResolveDaySegment(queuedAt);
        var directory = Path.Combine(_root.CloudTelemetrySpoolDirectory, day);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{DateTimeOffset.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
        WriteJson(path, payload);
        return _root.RelativePath(path);
    }

    public bool DeleteTelemetrySpoolItem(string relativePath)
    {
        var path = ResolveDataRootPath(relativePath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        DeleteEmptySpoolParents(Path.GetDirectoryName(path));
        return true;
    }

    public IReadOnlyList<string> EnumerateTelemetrySpoolItems(int maxItems)
    {
        if (!Directory.Exists(_root.CloudTelemetrySpoolDirectory))
        {
            return [];
        }

        var limit = Math.Clamp(maxItems, 1, 100);
        return Directory.EnumerateFiles(_root.CloudTelemetrySpoolDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(_root.RelativePath)
            .ToArray();
    }

    public Dictionary<string, object?> LoadTelemetrySpoolItem(string relativePath)
    {
        var path = ResolveDataRootPath(relativePath);
        return string.IsNullOrWhiteSpace(path) ? [] : ReadJson(path);
    }

    public string EnsureTelemetrySpoolDirectory()
    {
        Directory.CreateDirectory(_root.CloudTelemetrySpoolDirectory);
        return _root.RelativePath(_root.CloudTelemetrySpoolDirectory);
    }

    private static string ResolveDaySegment(string timestamp)
    {
        return DateTimeOffset.TryParse(timestamp, out var parsed)
            ? parsed.ToString("yyyyMMdd")
            : DateTimeOffset.Now.ToString("yyyyMMdd");
    }

    private string ResolveDataRootPath(string relativePath)
    {
        var cleaned = (relativePath ?? string.Empty).Trim().Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var path = Path.GetFullPath(Path.IsPathRooted(cleaned)
            ? cleaned
            : Path.Combine(_root.RootPath, cleaned));
        return _root.IsInsideDataRoot(path)
               && XkeDataRoot.IsPathInsideRoot(_root.CloudTelemetrySpoolDirectory, path)
            ? path
            : string.Empty;
    }

    private void DeleteEmptySpoolParents(string? directory)
    {
        var spoolRoot = Path.GetFullPath(_root.CloudTelemetrySpoolDirectory);
        var current = string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
        while (!string.IsNullOrWhiteSpace(current)
               && _root.IsInsideDataRoot(current)
               && !string.Equals(current, spoolRoot, StringComparison.OrdinalIgnoreCase)
               && XkeDataRoot.IsPathInsideRoot(spoolRoot, current))
        {
            if (Directory.EnumerateFileSystemEntries(current).Any())
            {
                return;
            }

            Directory.Delete(current);
            current = Path.GetDirectoryName(current);
        }
    }

    private static Dictionary<string, object?> ReadJson(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(new LocalEncryptedFileStore().ReadBytes(path));
            return LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
        }
        catch
        {
            return [];
        }
    }

    private void WriteJson(string path, object payload)
    {
        _encrypted.WriteJson(path, payload);
    }

    private string ResolveNoticeContentPath(string cacheFileName)
    {
        var cleaned = XkeDataRoot.SanitizePathSegment(cacheFileName ?? string.Empty);
        if (!cleaned.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            cleaned += ".md";
        }

        var path = Path.GetFullPath(Path.Combine(_root.CloudNoticeContentDirectory, cleaned));
        return XkeDataRoot.IsPathInsideRoot(_root.CloudNoticeContentDirectory, path) ? path : string.Empty;
    }
}

public sealed class CloudFeedCacheMeta
{
    [System.Text.Json.Serialization.JsonPropertyName("last_refreshed_at")]
    public string LastRefreshedAt { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
    public string ExpiresAt { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("content_fingerprint")]
    public string ContentFingerprint { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("stale")]
    public bool Stale { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;
}
