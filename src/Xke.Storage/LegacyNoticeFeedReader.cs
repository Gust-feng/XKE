// 本文件对应 Python `core/notice/service_v2.py` 的本地 notice feed 读取主线。
// 职责：从本地 notice cache 读取 index / payload / manifest / bundle / markdown，
// 拼装出可供后续控制平面消费的本地 feed 骨架，并在完整性失败时输出 fail-closed 风格结果。
using System.Text.Json;

namespace Xke.Storage;

public sealed class LegacyNoticeFeedReader
{
    private readonly LegacyNoticeCacheReader _cacheReader;

    public LegacyNoticeFeedReader(LegacyNoticeCacheReader cacheReader)
    {
        _cacheReader = cacheReader;
    }

    public LegacyNoticeFeedResult ReadLocalFeed(string appRoot, string channel, string version, string studentId = "", string? publicKeyPem = null)
    {
        var warnings = new List<string>();
        var indexValidation = _cacheReader.ReadAndValidateIndex(appRoot, publicKeyPem);
        if (!indexValidation.Exists || !indexValidation.HasValue)
        {
            return new LegacyNoticeFeedResult { Exists = false, SourcePath = indexValidation.SourcePath, Warnings = warnings.Append(indexValidation.Error).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() };
        }

        if (!indexValidation.SignatureValid)
        {
            warnings.Add(indexValidation.Error);
            var failClosedFeed = LegacyNoticeFeedBuilder.ApplyFailClosedDefaults(new Dictionary<string, object?>
            {
                ["source"] = "local-cache",
                ["channel"] = string.IsNullOrWhiteSpace(channel) ? "beta" : channel.Trim().ToLowerInvariant(),
                ["version"] = version?.Trim() ?? string.Empty,
                ["config"] = new Dictionary<string, object?>(),
            });
            return new LegacyNoticeFeedResult
            {
                Exists = false,
                SourcePath = indexValidation.SourcePath,
                Index = LegacyNoticeJson.ToDictionary(indexValidation.Value),
                Feed = failClosedFeed,
                Config = failClosedFeed.GetValueOrDefault("config") as Dictionary<string, object?> ?? new Dictionary<string, object?>(),
                Warnings = warnings,
            };
        }

        var indexPayload = LegacyNoticeJson.ToDictionary(indexValidation.Value);
        var noticeEntry = indexPayload.TryGetValue("notice", out var rawNoticeEntry) && rawNoticeEntry is Dictionary<string, object?> noticeDict ? noticeDict : new Dictionary<string, object?>();
        var versionPolicyEntry = indexPayload.TryGetValue("version_policy", out var rawVersionPolicyEntry) && rawVersionPolicyEntry is Dictionary<string, object?> versionPolicyDict ? versionPolicyDict : new Dictionary<string, object?>();
        var updateInfoEntry = indexPayload.TryGetValue("update_info", out var rawUpdateInfoEntry) && rawUpdateInfoEntry is Dictionary<string, object?> updateInfoDict ? updateInfoDict : new Dictionary<string, object?>();
        var accessPolicyEntry = indexPayload.TryGetValue("access_policy", out var rawAccessPolicyEntry) && rawAccessPolicyEntry is Dictionary<string, object?> accessPolicyDict ? accessPolicyDict : new Dictionary<string, object?>();

        var noticeDirectory = ReadPayload(noticeEntry, "本地公告目录", warnings);
        var versionPolicy = ReadPayload(versionPolicyEntry, "本地版本策略", warnings);
        var updateInfo = ReadPayload(updateInfoEntry, "本地更新信息", warnings);
        var accessPolicy = ReadPayload(accessPolicyEntry, "本地访问策略", warnings);

        var manifestEntry = SelectManifest(noticeDirectory, channel);
        var manifest = new Dictionary<string, object?>();
        var bundleRecords = new List<LegacyNoticeBundleRecord>();
        if (manifestEntry.Count > 0)
        {
            manifest = ReadPayload(manifestEntry, $"本地公告版本清单 {manifestEntry.GetValueOrDefault("id") ?? channel}", warnings);
            if (manifest.TryGetValue("bundles", out var rawBundles) && rawBundles is List<object?> bundleList)
            {
                foreach (var rawBundle in bundleList.OfType<Dictionary<string, object?>>())
                {
                    var bundleEntry = NormalizeBundleEntry(rawBundle);
                    var bundlePayload = ReadPayload(bundleEntry, $"公告集 {bundleEntry.GetValueOrDefault("bundle_id") ?? string.Empty}", warnings);
                    var hydrated = HydrateMarkdown(bundleEntry, bundlePayload, warnings);
                    bundleRecords.Add(new LegacyNoticeBundleRecord { Entry = bundleEntry, Bundle = hydrated });
                }
            }
        }

        var feed = LegacyNoticeFeedBuilder.Build(
            manifest,
            bundleRecords,
            channel,
            version,
            versionPolicy,
            updateInfo,
            accessPolicy,
            indexPayload,
            manifestEntry,
            "local-cache",
            studentId);

        return new LegacyNoticeFeedResult
        {
            Exists = true,
            SourcePath = indexValidation.SourcePath,
            Feed = feed,
            Index = indexPayload,
            Config = feed.TryGetValue("config", out var config) && config is Dictionary<string, object?> configDict ? configDict : new Dictionary<string, object?>(),
            Warnings = warnings,
        };
    }

    private Dictionary<string, object?> ReadPayload(Dictionary<string, object?> entry, string label, List<string> warnings)
    {
        var path = entry.GetValueOrDefault("path")?.ToString() ?? string.Empty;
        var sha = entry.GetValueOrDefault("sha256")?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return new Dictionary<string, object?>();

        var result = _cacheReader.ReadPayloadJsonValidated(path, sha);
        if (!result.Exists || !result.HasValue)
        {
            warnings.Add(string.IsNullOrWhiteSpace(result.Error) ? $"{label}缺失" : $"{label}读取失败：{result.Error}");
            return new Dictionary<string, object?>();
        }
        if (!result.HashValid)
        {
            warnings.Add(string.IsNullOrWhiteSpace(result.Error) ? $"{label}校验失败" : $"{label}{result.Error}");
            return new Dictionary<string, object?>();
        }
        return LegacyNoticeJson.ToDictionary(result.Value);
    }

    private static Dictionary<string, object?> SelectManifest(Dictionary<string, object?> directoryPayload, string channel)
    {
        if (directoryPayload.TryGetValue("manifests", out var rawManifests) && rawManifests is List<object?> manifests)
        {
            var channelText = (channel ?? string.Empty).Trim().ToLowerInvariant();
            foreach (var item in manifests.OfType<Dictionary<string, object?>>())
            {
                var id = (item.GetValueOrDefault("id") ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(channelText) && id == channelText)
                {
                    return item;
                }
            }
        }
        return new Dictionary<string, object?>();
    }

    private static Dictionary<string, object?> NormalizeBundleEntry(Dictionary<string, object?> rawEntry)
    {
        var bundleId = (rawEntry.GetValueOrDefault("bundle_id") ?? rawEntry.GetValueOrDefault("id") ?? string.Empty).ToString() ?? string.Empty;
        var path = LegacyNoticePaths.NormalizeRelativePath(rawEntry.GetValueOrDefault("path")?.ToString() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(bundleId) && !string.IsNullOrWhiteSpace(path))
        {
            bundleId = Path.GetFileNameWithoutExtension(path);
        }
        return new Dictionary<string, object?>
        {
            ["bundle_id"] = bundleId,
            ["path"] = path,
            ["updated_at"] = rawEntry.GetValueOrDefault("updated_at") ?? string.Empty,
            ["sha256"] = (rawEntry.GetValueOrDefault("sha256") ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty,
        };
    }

    private Dictionary<string, object?> HydrateMarkdown(Dictionary<string, object?> bundleEntry, Dictionary<string, object?> bundlePayload, List<string> warnings)
    {
        if (!bundlePayload.TryGetValue("items", out var rawItems) || rawItems is not List<object?> items)
        {
            return bundlePayload;
        }

        var hydratedItems = new List<object?>();
        var bundlePath = bundleEntry.GetValueOrDefault("path")?.ToString() ?? string.Empty;
        foreach (var rawItem in items.OfType<Dictionary<string, object?>>())
        {
            var item = new Dictionary<string, object?>(rawItem);
            var contentPath = item.GetValueOrDefault("content_path")?.ToString() ?? string.Empty;
            var contentSha = item.GetValueOrDefault("content_sha256")?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contentPath))
            {
                hydratedItems.Add(item);
                continue;
            }

            var resolvedPath = LegacyNoticePaths.ResolveRelatedRelativePath(bundlePath, contentPath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || resolvedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || resolvedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                hydratedItems.Add(item);
                continue;
            }

            var textResult = _cacheReader.ReadPayloadText(resolvedPath);
            if (!textResult.Exists || !textResult.HasValue || string.IsNullOrWhiteSpace(textResult.Value))
            {
                warnings.Add($"公告正文读取失败：{resolvedPath}");
                hydratedItems.Add(item);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(contentSha))
            {
                var markdownBytes = NoticeCompatibility.NoticeTextBytes(textResult.Value);
                var actualSha = Xke.Security.NoticeHash.Sha256Hex(markdownBytes);
                if (!string.Equals(contentSha.Trim().ToLowerInvariant(), actualSha, StringComparison.Ordinal))
                {
                    warnings.Add($"公告正文校验失败：{resolvedPath}");
                    hydratedItems.Add(item);
                    continue;
                }
            }

            item["content_resolved_path"] = resolvedPath;
            item["content_markdown"] = textResult.Value;
            if (string.IsNullOrWhiteSpace(item.GetValueOrDefault("content")?.ToString()))
            {
                item["content"] = textResult.Value;
            }
            hydratedItems.Add(item);
        }

        var merged = new Dictionary<string, object?>(bundlePayload)
        {
            ["items"] = hydratedItems,
        };
        return merged;
    }
}
