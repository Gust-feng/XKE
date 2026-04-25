using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Core.Notice;
using Xke.Security;
using Xke.Storage;

namespace Xke.Core.CloudSync;

public sealed class CloudFeedService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

    private readonly XkeDataRoot _dataRoot;
    private readonly NoticeServiceOptions _options;
    private readonly INoticeRemoteSource _remoteSource;
    private readonly RemotePolicyService _policyService;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public CloudFeedService(
        XkeDataRoot dataRoot,
        NoticeServiceOptions options,
        INoticeRemoteSource remoteSource,
        RemotePolicyService policyService)
    {
        _dataRoot = dataRoot;
        _options = options;
        _remoteSource = remoteSource;
        _policyService = policyService;
    }

    public async Task<Dictionary<string, object?>> LoadFeedAsync(
        string channel,
        string version,
        string trigger = "view",
        string studentId = "",
        CancellationToken cancellationToken = default)
    {
        var store = _dataRoot.CloudSync();
        var warnings = new List<string>();
        var meta = store.LoadFeedMeta();
        var package = store.LoadFeed();

        if (ShouldSync(package, meta, trigger))
        {
            await _syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                meta = store.LoadFeedMeta();
                package = store.LoadFeed();
                if (ShouldSync(package, meta, trigger))
                {
                    var syncResult = await SyncCloudFeedAsync(package, meta, cancellationToken).ConfigureAwait(false);
                    warnings.AddRange(syncResult.Warnings);
                    package = syncResult.Package.Count > 0 ? syncResult.Package : package;
                    meta = syncResult.Meta;
                }
            }
            finally
            {
                _syncGate.Release();
            }
        }

        if (package.Count == 0)
        {
            package = BuildDefaultPackage();
            meta = new CloudFeedCacheMeta
            {
                Stale = true,
                Source = "defaults",
            };
        }

        return await BuildClientFeedAsync(package, meta, warnings, channel, version, studentId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<string, object?>> ResolveAccessDecisionAsync(
        string studentId,
        string trigger = "view",
        CancellationToken cancellationToken = default)
    {
        var feed = await LoadFeedAsync(_options.DefaultChannel, string.Empty, trigger, studentId, cancellationToken).ConfigureAwait(false);
        return feed.GetValueOrDefault("access_decision") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["blocked"] = false,
                ["message"] = string.Empty,
                ["student_id_hash"] = string.Empty,
                ["reason"] = string.Empty,
            };
    }

    public Dictionary<string, object?> BuildBlockedDetail(string studentId, Dictionary<string, object?> decision)
    {
        return _policyService.BuildBlockedDetail(studentId, decision);
    }

    private async Task<(Dictionary<string, object?> Package, CloudFeedCacheMeta Meta, IReadOnlyList<string> Warnings)> SyncCloudFeedAsync(
        Dictionary<string, object?> currentPackage,
        CloudFeedCacheMeta currentMeta,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var now = DateTimeOffset.Now;
        var result = await _remoteSource.GetBytesAsync(_options.CloudFeedPath, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            warnings.Add($"云端入口读取失败：{result.Error}");
            var staleMeta = BuildMeta(currentMeta.ContentFingerprint, now, stale: currentPackage.Count > 0, source: currentPackage.Count > 0 ? "local-cache" : "missing");
            if (currentPackage.Count > 0)
            {
                _dataRoot.CloudSync().SaveFeedMeta(staleMeta);
            }
            return (currentPackage, staleMeta, warnings);
        }

        Dictionary<string, object?> remotePackage;
        try
        {
            using var document = JsonDocument.Parse(result.Content);
            remotePackage = LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
        }
        catch (Exception ex)
        {
            warnings.Add($"云端单包解析失败：{ex.Message}");
            var staleMeta = BuildMeta(currentMeta.ContentFingerprint, now, stale: currentPackage.Count > 0, source: currentPackage.Count > 0 ? "local-cache" : "invalid");
            if (currentPackage.Count > 0)
            {
                _dataRoot.CloudSync().SaveFeedMeta(staleMeta);
            }
            return (currentPackage, staleMeta, warnings);
        }

        if (!remotePackage.ContainsKey("announcement")
            || !remotePackage.ContainsKey("policy")
            || !remotePackage.ContainsKey("update_info"))
        {
            warnings.Add("云端单包根结构缺少 announcement、policy 或 update_info");
            var staleMeta = BuildMeta(currentMeta.ContentFingerprint, now, stale: currentPackage.Count > 0, source: currentPackage.Count > 0 ? "local-cache" : "invalid");
            if (currentPackage.Count > 0)
            {
                _dataRoot.CloudSync().SaveFeedMeta(staleMeta);
            }
            return (currentPackage, staleMeta, warnings);
        }

        var fingerprint = Fingerprint(remotePackage);
        var meta = BuildMeta(fingerprint, now, stale: false, source: "cloud");
        var store = _dataRoot.CloudSync();
        if (!string.Equals(fingerprint, currentMeta.ContentFingerprint, StringComparison.Ordinal))
        {
            store.SaveFeed(remotePackage);
        }
        store.SaveFeedMeta(meta);
        return (remotePackage, meta, warnings);
    }

    private async Task<Dictionary<string, object?>> BuildClientFeedAsync(
        Dictionary<string, object?> package,
        CloudFeedCacheMeta meta,
        List<string> warnings,
        string channel,
        string version,
        string studentId,
        CancellationToken cancellationToken)
    {
        var announcement = GetDictionary(package, "announcement");
        var policy = GetDictionary(package, "policy");
        var updateInfo = GetDictionary(package, "update_info");
        var config = _policyService.NormalizePolicy(policy, updateInfo);
        var accessDecision = _policyService.ResolveAccessDecision(config, studentId);
        if (accessDecision.GetValueOrDefault("blocked") is true)
        {
            var appControl = config.GetValueOrDefault("app_control") as Dictionary<string, object?> ?? [];
            appControl["state"] = "blocked";
            appControl["reason"] = "access-policy-blocked";
            config["app_control"] = appControl;
        }

        config["access_decision"] = accessDecision;
        var items = await BuildAnnouncementItemsAsync(announcement, warnings, cancellationToken).ConfigureAwait(false);
        var panelItems = items
            .OfType<Dictionary<string, object?>>()
            .Where(item => Text(item.GetValueOrDefault("display_mode")) is "" or "panel")
            .Cast<object?>()
            .ToList();
        var reminderItems = items
            .OfType<Dictionary<string, object?>>()
            .Where(item => Text(item.GetValueOrDefault("display_mode")) == "reminder")
            .Cast<object?>()
            .ToList();

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = Text(package.GetValueOrDefault("schema_version")) is { Length: > 0 } schema ? schema : "1",
            ["source"] = meta.Stale ? "local-cache" : meta.Source,
            ["stale"] = meta.Stale,
            ["channel"] = string.IsNullOrWhiteSpace(channel) ? _options.DefaultChannel : channel.Trim(),
            ["version"] = version ?? string.Empty,
            ["updated_at"] = Text(package.GetValueOrDefault("updated_at")),
            ["last_refreshed_at"] = meta.LastRefreshedAt,
            ["expires_at"] = meta.ExpiresAt,
            ["warnings"] = warnings.Cast<object?>().ToList(),
            ["announcement"] = announcement,
            ["notice"] = announcement,
            ["items"] = items,
            ["panel_items"] = panelItems,
            ["reminder_items"] = reminderItems,
            ["config"] = config,
            ["policy"] = policy,
            ["version_policy"] = config.GetValueOrDefault("version_policy"),
            ["update_info"] = config.GetValueOrDefault("update_info"),
            ["access_policy"] = config.GetValueOrDefault("access_policy"),
            ["access_decision"] = accessDecision,
        };
    }

    private async Task<List<object?>> BuildAnnouncementItemsAsync(
        Dictionary<string, object?> announcement,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var sourceItems = GetList(announcement, "items");
        if (sourceItems.Count == 0)
        {
            return sourceItems;
        }

        var store = _dataRoot.CloudSync();
        var contentMeta = store.LoadNoticeContentMeta();
        var contentMetaItems = EnsureDictionary(contentMeta, "items");
        var activeMetaKeys = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;
        var result = new List<object?>();

        foreach (var rawItem in sourceItems)
        {
            if (rawItem is not Dictionary<string, object?> item)
            {
                result.Add(rawItem);
                continue;
            }

            var normalized = new Dictionary<string, object?>(item, StringComparer.Ordinal);
            var contentPath = NormalizeRemotePath(Text(normalized.GetValueOrDefault("content_path")));
            if (string.IsNullOrWhiteSpace(contentPath))
            {
                normalized["content"] = Text(normalized.GetValueOrDefault("content"));
                result.Add(normalized);
                continue;
            }
            normalized["content_path"] = contentPath;

            var itemId = Text(normalized.GetValueOrDefault("id"));
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = BuildAnonymousContentId(contentPath);
                normalized["id"] = itemId;
            }

            var updatedAt = Text(normalized.GetValueOrDefault("updated_at"));
            var metaKey = BuildContentMetaKey(itemId, contentPath);
            activeMetaKeys.Add(metaKey);
            var expectedFingerprint = BuildContentFingerprint(itemId, updatedAt, contentPath);
            var existingMeta = GetDictionary(contentMetaItems, metaKey);
            var cacheFile = Text(existingMeta.GetValueOrDefault("cache_file"));
            if (string.IsNullOrWhiteSpace(cacheFile))
            {
                cacheFile = BuildContentCacheFileName(itemId, contentPath);
            }

            if (string.Equals(Text(existingMeta.GetValueOrDefault("fingerprint")), expectedFingerprint, StringComparison.Ordinal)
                && store.TryLoadNoticeContent(cacheFile, out var cachedContent))
            {
                normalized["content"] = cachedContent;
                normalized["content_cached"] = true;
                result.Add(normalized);
                continue;
            }

            var remote = await _remoteSource.GetBytesAsync(contentPath, cancellationToken).ConfigureAwait(false);
            if (remote.Success)
            {
                var content = Encoding.UTF8.GetString(remote.Content);
                store.SaveNoticeContent(cacheFile, content);
                contentMetaItems[metaKey] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = itemId,
                    ["content_path"] = contentPath,
                    ["updated_at"] = updatedAt,
                    ["fingerprint"] = expectedFingerprint,
                    ["cache_file"] = cacheFile,
                    ["cached_at"] = DateTimeOffset.Now.ToString("O"),
                };
                changed = true;
                normalized["content"] = content;
                normalized["content_cached"] = false;
                result.Add(normalized);
                continue;
            }

            if ((!string.IsNullOrWhiteSpace(cacheFile)
                 && store.TryLoadNoticeContent(cacheFile, out var staleContent))
                || TryLoadAnyCachedContentForItem(store, contentMetaItems, itemId, out staleContent))
            {
                warnings.Add($"公告正文读取失败，使用本地缓存：{contentPath}：{remote.Error}");
                normalized["content"] = staleContent;
                normalized["content_cached"] = true;
                normalized["content_stale"] = true;
                result.Add(normalized);
                continue;
            }

            warnings.Add($"公告正文读取失败：{contentPath}：{remote.Error}");
            normalized["content"] = string.Empty;
            normalized["content_missing"] = true;
            result.Add(normalized);
        }

        foreach (var staleKey in contentMetaItems.Keys.Where(key => !activeMetaKeys.Contains(key)).ToArray())
        {
            contentMetaItems.Remove(staleKey);
            changed = true;
        }

        if (changed)
        {
            contentMeta["schema_version"] = "1";
            contentMeta["updated_at"] = DateTimeOffset.Now.ToString("O");
            store.SaveNoticeContentMeta(contentMeta);
        }

        return result;
    }

    private static bool TryLoadAnyCachedContentForItem(
        CloudSyncStore store,
        Dictionary<string, object?> contentMetaItems,
        string itemId,
        out string content)
    {
        content = string.Empty;
        foreach (var rawEntry in contentMetaItems.Values)
        {
            if (rawEntry is not Dictionary<string, object?> entry)
            {
                continue;
            }

            if (!string.Equals(Text(entry.GetValueOrDefault("id")), itemId, StringComparison.Ordinal))
            {
                continue;
            }

            var cacheFile = Text(entry.GetValueOrDefault("cache_file"));
            if (store.TryLoadNoticeContent(cacheFile, out content))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldSync(Dictionary<string, object?> package, CloudFeedCacheMeta meta, string trigger)
    {
        if (string.Equals(trigger, "manual", StringComparison.OrdinalIgnoreCase)
            || trigger.StartsWith("manual/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (package.Count == 0)
        {
            return true;
        }
        return !DateTimeOffset.TryParse(meta.ExpiresAt, out var expiresAt) || DateTimeOffset.Now >= expiresAt;
    }

    private static CloudFeedCacheMeta BuildMeta(string fingerprint, DateTimeOffset now, bool stale, string source)
    {
        return new CloudFeedCacheMeta
        {
            LastRefreshedAt = now.ToString("O"),
            ExpiresAt = now.Add(CacheDuration).ToString("O"),
            ContentFingerprint = fingerprint,
            Stale = stale,
            Source = source,
        };
    }

    private static Dictionary<string, object?> BuildDefaultPackage()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["updated_at"] = string.Empty,
            ["announcement"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = string.Empty,
                ["subtitle"] = string.Empty,
                ["notice_text"] = string.Empty,
                ["items"] = new List<object?>(),
            },
            ["policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["update_info"] = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static string Fingerprint(Dictionary<string, object?> payload)
    {
        return Convert.ToHexString(SHA256.HashData(NoticeCanonicalJson.Serialize(payload))).ToLowerInvariant();
    }

    private static Dictionary<string, object?> GetDictionary(Dictionary<string, object?> parent, string key)
    {
        return parent.GetValueOrDefault(key) is Dictionary<string, object?> dictionary ? dictionary : [];
    }

    private static Dictionary<string, object?> EnsureDictionary(Dictionary<string, object?> parent, string key)
    {
        if (parent.TryGetValue(key, out var raw) && raw is Dictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        var created = new Dictionary<string, object?>(StringComparer.Ordinal);
        parent[key] = created;
        return created;
    }

    private static List<object?> GetList(Dictionary<string, object?> parent, string key)
    {
        var value = parent.GetValueOrDefault(key);
        if (value is List<object?> list)
        {
            return list;
        }

        if (value is IEnumerable<object?> enumerable)
        {
            return enumerable.ToList();
        }

        if (value is System.Collections.IEnumerable rawValues)
        {
            return rawValues.Cast<object?>().ToList();
        }

        return [];
    }

    private static string Text(object? value)
    {
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string NormalizeRemotePath(string path)
    {
        return (path ?? string.Empty).Trim().TrimStart('/').Replace('\\', '/');
    }

    private static string BuildAnonymousContentId(string contentPath)
    {
        return $"notice-{Sha256Hex(contentPath)[..12]}";
    }

    private static string BuildContentMetaKey(string itemId, string contentPath)
    {
        return Sha256Hex($"{itemId.Trim()}|{contentPath.Trim()}");
    }

    private static string BuildContentFingerprint(string itemId, string updatedAt, string contentPath)
    {
        return Sha256Hex($"{itemId.Trim()}|{updatedAt.Trim()}|{contentPath.Trim()}");
    }

    private static string BuildContentCacheFileName(string itemId, string contentPath)
    {
        var safeId = XkeDataRoot.SanitizePathSegment(itemId);
        return $"{safeId}-{Sha256Hex(contentPath)[..12]}.md";
    }

    private static string Sha256Hex(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty))).ToLowerInvariant();
    }
}
