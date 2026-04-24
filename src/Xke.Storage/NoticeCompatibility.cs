using System.Globalization;
using System.Text;
using Xke.Security;

namespace Xke.Storage;

public static class NoticeCompatibility
{
    public const string DefaultAccessPolicyMessage = "当前账号已被停止使用，如有疑问请联系开发者。";

    private static readonly HashSet<string> LifecycleStages = new(StringComparer.Ordinal)
    {
        "maintenance",
        "deprecated",
    };

    private static readonly HashSet<string> AppControlStates = new(StringComparer.Ordinal)
    {
        "normal",
        "readonly",
        "blocked",
    };

    private static readonly IReadOnlyDictionary<string, object?> DefaultFeatureFlags = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["schedule_view"] = true,
        ["public_course_preview"] = true,
        ["course_grab"] = false,
        ["campus_login"] = true,
        ["keepalive"] = true,
    };

    private static readonly IReadOnlyDictionary<string, object?> DefaultFeatureLimits = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["course_grab"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["min_interval_sec"] = 0.5d,
            ["max_runtime_sec"] = 5,
            ["cooldown_sec"] = 86400,
        },
    };

    private static readonly IReadOnlyDictionary<string, object?> DefaultRuntimePolicy = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["config_pull_interval_sec"] = 21600,
        ["login_sync_cooldown_sec"] = 300,
        ["summary_report_interval_sec"] = 3600,
    };

    public static Dictionary<string, object?> NormalizeNoticeIndex(Dictionary<string, object?> rawIndex)
    {
        var payload = CloneDictionary(rawIndex);
        var noticeEntry = payload.TryGetValue("notice", out var noticeRaw) ? GetDictionary(noticeRaw) : [];
        if (noticeEntry.Count == 0)
        {
            noticeEntry = payload.TryGetValue("notice_catalog", out var noticeCatalogRaw) ? GetDictionary(noticeCatalogRaw) : [];
        }

        var versionPolicyEntry = payload.TryGetValue("version_policy", out var versionPolicyRaw) ? GetDictionary(versionPolicyRaw) : [];
        if (versionPolicyEntry.Count == 0)
        {
            versionPolicyEntry = payload.TryGetValue("software_policy", out var softwarePolicyRaw) ? GetDictionary(softwarePolicyRaw) : [];
        }
        if (versionPolicyEntry.Count == 0)
        {
            versionPolicyEntry = payload.TryGetValue("app_policy", out var appPolicyRaw) ? GetDictionary(appPolicyRaw) : [];
        }

        var updateInfoEntry = payload.TryGetValue("update_info", out var updateInfoRaw) ? GetDictionary(updateInfoRaw) : [];
        if (updateInfoEntry.Count == 0)
        {
            updateInfoEntry = payload.TryGetValue("release_policy", out var releasePolicyRaw) ? GetDictionary(releasePolicyRaw) : [];
        }

        var accessPolicyEntry = payload.TryGetValue("access_policy", out var accessPolicyRaw) ? GetDictionary(accessPolicyRaw) : [];

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = Math.Max(1, SafeInt(payload.GetValueOrDefault("schema_version"), 1)),
            ["updated_at"] = NormalizeIsoText(payload.GetValueOrDefault("updated_at")),
            ["notice"] = NormalizeIndexEntry(noticeEntry),
            ["version_policy"] = NormalizeIndexEntry(versionPolicyEntry),
            ["update_info"] = NormalizeIndexEntry(updateInfoEntry),
            ["access_policy"] = NormalizeIndexEntry(accessPolicyEntry),
            ["notice_catalog"] = NormalizeIndexEntry(noticeEntry),
            ["software_policy"] = NormalizeIndexEntry(versionPolicyEntry),
            ["app_policy"] = NormalizeIndexEntry(versionPolicyEntry),
            ["release_policy"] = NormalizeIndexEntry(updateInfoEntry),
        };
    }

    public static Dictionary<string, object?> NormalizeIndexEntry(Dictionary<string, object?> rawEntry)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = LegacyNoticePaths.NormalizeRelativePath(rawEntry.GetValueOrDefault("path")?.ToString()),
            ["updated_at"] = NormalizeIsoText(rawEntry.GetValueOrDefault("updated_at") ?? rawEntry.GetValueOrDefault("published_at")),
            ["sha256"] = NormalizeSha(rawEntry.GetValueOrDefault("sha256")),
        };
    }

    public static Dictionary<string, object?> NormalizeNoticeDirectory(Dictionary<string, object?> rawDirectory)
    {
        var payload = CloneDictionary(rawDirectory);
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = NormalizeSchemaVersion(payload.GetValueOrDefault("schema_version"), "1"),
            ["updated_at"] = NormalizeIsoText(payload.GetValueOrDefault("updated_at")),
            ["manifests"] = new List<object?>(),
        };

        foreach (var rawItem in GetList(payload.GetValueOrDefault("manifests")).OfType<Dictionary<string, object?>>())
        {
            var manifest = NormalizeManifestEntry(rawItem);
            if (!string.IsNullOrWhiteSpace(manifest.GetValueOrDefault("path")?.ToString()))
            {
                ((List<object?>)normalized["manifests"]!).Add(manifest);
            }
        }

        return normalized;
    }

    public static Dictionary<string, object?> NormalizeNoticeCatalog(Dictionary<string, object?> rawManifest)
    {
        var payload = CloneDictionary(rawManifest);
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = NormalizeSchemaVersion(payload.GetValueOrDefault("schema_version"), "1"),
            ["manifest_id"] = TextFromKeys(payload, "manifest_id", "id", "channel").ToLowerInvariant()[..Math.Min(TextFromKeys(payload, "manifest_id", "id", "channel").Length, 128)],
            ["updated_at"] = NormalizeIsoText(payload.GetValueOrDefault("updated_at")),
            ["title"] = TrimTo(TextFromKeys(payload, "title", "name"), 64),
            ["subtitle"] = TrimTo(TextFromKeys(payload, "subtitle", "description"), 512),
            ["notice_text"] = TrimTo(TextFromKeys(payload, "notice_text", "summary", "content"), 20000),
            ["bundles"] = new List<object?>(),
        };

        foreach (var rawItem in GetList(payload.GetValueOrDefault("bundles")).OfType<Dictionary<string, object?>>())
        {
            var bundle = NormalizeBundleEntry(rawItem);
            if (!string.IsNullOrWhiteSpace(bundle.GetValueOrDefault("bundle_id")?.ToString())
                && !string.IsNullOrWhiteSpace(bundle.GetValueOrDefault("path")?.ToString()))
            {
                ((List<object?>)normalized["bundles"]!).Add(bundle);
            }
        }

        return normalized;
    }

    public static Dictionary<string, object?> NormalizeNoticeBundle(Dictionary<string, object?> rawBundle)
    {
        var payload = CloneDictionary(rawBundle);
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = NormalizeSchemaVersion(payload.GetValueOrDefault("schema_version"), "1"),
            ["bundle_id"] = TrimTo(TextFromKeys(payload, "bundle_id", "id", "key", "slug"), 128),
            ["updated_at"] = NormalizeIsoText(payload.GetValueOrDefault("updated_at")),
            ["title"] = TrimTo(TextFromKeys(payload, "title", "name"), 128),
            ["summary"] = TrimTo(TextFromKeys(payload, "summary", "subtitle", "description"), 512),
            ["items"] = new List<object?>(),
        };

        foreach (var rawItem in GetList(payload.GetValueOrDefault("items")).OfType<Dictionary<string, object?>>())
        {
            var item = NormalizeNoticeItem(rawItem);
            if (!string.IsNullOrWhiteSpace(item.GetValueOrDefault("id")?.ToString()))
            {
                ((List<object?>)normalized["items"]!).Add(item);
            }
        }

        return normalized;
    }

    public static Dictionary<string, object?> NormalizeVersionPolicyConfig(Dictionary<string, object?> rawConfig)
    {
        var config = CloneDictionary(rawConfig);
        if (!config.ContainsKey("versions")
            && (config.ContainsKey("lifecycle")
                || config.ContainsKey("app_control")
                || config.ContainsKey("feature_flags")
                || config.ContainsKey("feature_limits")
                || config.ContainsKey("runtime_policy")
                || config.ContainsKey("telemetry_enabled")))
        {
            config = LegacyAppPolicyToVersionPolicy(config);
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = NormalizeSchemaVersion(config.GetValueOrDefault("schema_version"), "1"),
            ["updated_at"] = NormalizeIsoText(config.GetValueOrDefault("updated_at")),
            ["versions"] = new List<object?>(),
        };

        foreach (var rawItem in GetList(config.GetValueOrDefault("versions")).OfType<Dictionary<string, object?>>())
        {
            var normalized = NormalizeVersionPolicyEntry(rawItem);
            if (normalized.Count > 0)
            {
                ((List<object?>)payload["versions"]!).Add(normalized);
            }
        }

        return payload;
    }

    public static Dictionary<string, object?> NormalizeUpdateInfoConfig(Dictionary<string, object?> rawConfig)
    {
        var config = CloneDictionary(rawConfig);
        if (!config.ContainsKey("latest_version")
            && !config.ContainsKey("min_supported_version")
            && !config.ContainsKey("download_url")
            && GetDictionary(config.GetValueOrDefault("release")).Count > 0)
        {
            config = LegacyReleasePolicyToUpdateInfo(config);
        }

        if (!config.ContainsKey("latest_version") && GetList(config.GetValueOrDefault("channels")).Count > 0)
        {
            var firstChannel = GetList(config.GetValueOrDefault("channels")).OfType<Dictionary<string, object?>>().FirstOrDefault() ?? [];
            config = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema_version"] = config.GetValueOrDefault("schema_version") ?? "1",
                ["updated_at"] = config.GetValueOrDefault("updated_at") ?? firstChannel.GetValueOrDefault("updated_at") ?? string.Empty,
                ["latest_version"] = firstChannel.GetValueOrDefault("latest_version") ?? string.Empty,
                ["min_supported_version"] = firstChannel.GetValueOrDefault("min_supported_version") ?? string.Empty,
                ["download_url"] = firstChannel.GetValueOrDefault("download_url") ?? string.Empty,
            };
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = NormalizeSchemaVersion(config.GetValueOrDefault("schema_version"), "1"),
            ["updated_at"] = NormalizeIsoText(config.GetValueOrDefault("updated_at")),
            ["latest_version"] = TrimTo(config.GetValueOrDefault("latest_version")?.ToString() ?? string.Empty, 64),
            ["min_supported_version"] = TrimTo(config.GetValueOrDefault("min_supported_version")?.ToString() ?? string.Empty, 64),
            ["download_url"] = TrimTo(config.GetValueOrDefault("download_url")?.ToString() ?? string.Empty, 2000),
        };

        return payload;
    }

    public static Dictionary<string, object?> NormalizeAccessPolicy(Dictionary<string, object?> rawPolicy)
    {
        var blockedStudents = new List<object?>();
        var blockedHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawItem in GetList(rawPolicy.GetValueOrDefault("blocked_students")).OfType<Dictionary<string, object?>>())
        {
            var hash = TrimTo(
                (rawItem.GetValueOrDefault("student_id_hash")
                    ?? rawItem.GetValueOrDefault("hash")
                    ?? rawItem.GetValueOrDefault("id")
                    ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty,
                64);

            if (hash.Length != 64 || !blockedHashes.Add(hash))
            {
                continue;
            }

            blockedStudents.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["student_id_hash"] = hash,
                ["message"] = TrimTo(rawItem.GetValueOrDefault("message")?.ToString() ?? string.Empty, 512),
            });
        }

        foreach (var rawItem in GetList(rawPolicy.GetValueOrDefault("blocked_student_hashes")))
        {
            var hash = TrimTo((rawItem ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty, 64);
            if (hash.Length == 64 && blockedHashes.Add(hash))
            {
                blockedStudents.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["student_id_hash"] = hash,
                    ["message"] = string.Empty,
                });
            }
        }

        var defaultMessage = TrimTo(
            (rawPolicy.GetValueOrDefault("default_message") ?? rawPolicy.GetValueOrDefault("message") ?? string.Empty).ToString()?.Trim() ?? string.Empty,
            512);

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = Math.Max(1, SafeInt(rawPolicy.GetValueOrDefault("schema_version"), 1)),
            ["updated_at"] = NormalizeIsoText(rawPolicy.GetValueOrDefault("updated_at")),
            ["default_message"] = defaultMessage,
            ["message"] = defaultMessage,
            ["blocked_students"] = blockedStudents,
            ["blocked_student_hashes"] = blockedHashes.Cast<object?>().ToList(),
        };
    }

    public static Dictionary<string, object?> ResolveAccessPolicyDecision(Dictionary<string, object?> accessPolicy, string studentId)
    {
        var normalized = NormalizeAccessPolicy(accessPolicy);
        var normalizedStudentId = (studentId ?? string.Empty).Trim();
        var studentHash = string.IsNullOrWhiteSpace(normalizedStudentId)
            ? string.Empty
            : NoticeHash.Sha256Hex(Encoding.UTF8.GetBytes(normalizedStudentId));

        var blockedHashSet = new HashSet<string>(
            GetList(normalized.GetValueOrDefault("blocked_student_hashes"))
                .Select(item => (item ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty)
                .Where(value => value.Length == 64),
            StringComparer.Ordinal);

        var matchedRecord = GetList(normalized.GetValueOrDefault("blocked_students"))
            .OfType<Dictionary<string, object?>>()
            .FirstOrDefault(item => string.Equals(
                item.GetValueOrDefault("student_id_hash")?.ToString()?.Trim().ToLowerInvariant(),
                studentHash,
                StringComparison.Ordinal))
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);

        var blocked = !string.IsNullOrWhiteSpace(studentHash) && blockedHashSet.Contains(studentHash);
        var message = TrimTo(
            (matchedRecord.GetValueOrDefault("message")
                ?? normalized.GetValueOrDefault("default_message")
                ?? normalized.GetValueOrDefault("message")
                ?? DefaultAccessPolicyMessage).ToString()?.Trim() ?? DefaultAccessPolicyMessage,
            512);

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["student_id_hash"] = studentHash,
            ["blocked"] = blocked,
            ["message"] = blocked ? message : string.Empty,
        };
    }

    public static Dictionary<string, object?> FailClosedConfig()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["feature_flags"] = CloneDictionary(DefaultFeatureFlags),
            ["feature_limits"] = CloneDictionary(DefaultFeatureLimits),
            ["runtime_policy"] = CloneDictionary(DefaultRuntimePolicy),
            ["lifecycle"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["stage"] = "maintenance",
            },
            ["app_control"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = "readonly",
                ["reason"] = "notice-integrity-check-failed",
            },
            ["status_notice"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["message"] = string.Empty,
            },
        };
    }

    public static Dictionary<string, object?> SelectNoticeManifest(Dictionary<string, object?> directoryPayload, string channel)
    {
        var normalized = NormalizeNoticeDirectory(directoryPayload);
        var channelText = (channel ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var rawItem in GetList(normalized.GetValueOrDefault("manifests")).OfType<Dictionary<string, object?>>())
        {
            if (string.Equals(rawItem.GetValueOrDefault("id")?.ToString()?.Trim().ToLowerInvariant(), channelText, StringComparison.Ordinal))
            {
                return rawItem;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public static Dictionary<string, object?> ApplyFailClosedDefaults(Dictionary<string, object?> feed)
    {
        var safePolicy = SelectVersionPolicyEntry(FailClosedVersionPolicy(), string.Empty, string.Empty).MatchedPolicy;
        var config = GetDictionary(feed.GetValueOrDefault("config"));
        config["feature_flags"] = CloneDictionary(GetDictionary(safePolicy.GetValueOrDefault("feature_flags")));
        config["feature_limits"] = CloneDictionary(GetDictionary(safePolicy.GetValueOrDefault("feature_limits")));
        config["runtime_policy"] = CloneDictionary(GetDictionary(safePolicy.GetValueOrDefault("runtime_policy")));
        config["telemetry"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enabled"] = true,
            ["config_pull_interval_sec"] = GetDictionary(safePolicy.GetValueOrDefault("runtime_policy")).GetValueOrDefault("config_pull_interval_sec") ?? 21600,
            ["summary_report_interval_sec"] = GetDictionary(safePolicy.GetValueOrDefault("runtime_policy")).GetValueOrDefault("summary_report_interval_sec") ?? 3600,
        };
        config["lifecycle"] = CloneDictionary(GetDictionary(safePolicy.GetValueOrDefault("lifecycle")));
        config["app_control"] = CloneDictionary(GetDictionary(safePolicy.GetValueOrDefault("app_control")));
        config["status_notice"] = NormalizeStatusNotice([], safePolicy.GetValueOrDefault("lifecycle"), safePolicy.GetValueOrDefault("app_control"));
        config["version_policy_match"] = CloneDictionary(safePolicy);
        config["version_policy"] = CloneDictionary(safePolicy);
        config["software_policy"] = CloneDictionary(safePolicy);
        config["app_policy"] = CloneDictionary(safePolicy);
        feed["config"] = config;
        feed["version_policy_match"] = CloneDictionary(safePolicy);
        feed["version_policy"] = CloneDictionary(safePolicy);
        feed["software_policy"] = CloneDictionary(safePolicy);
        feed["app_policy"] = CloneDictionary(safePolicy);
        return feed;
    }

    public static Dictionary<string, object?> BuildNoticeFeedPayload(
        Dictionary<string, object?> noticeCatalog,
        List<LegacyNoticeBundleRecord> bundles,
        string channel,
        string version,
        Dictionary<string, object?> versionPolicy,
        Dictionary<string, object?> updateInfo,
        Dictionary<string, object?> accessPolicy,
        Dictionary<string, object?> indexPayload,
        Dictionary<string, object?> manifestEntry,
        string source,
        string studentId)
    {
        var normalizedManifest = NormalizeNoticeCatalog(noticeCatalog);
        var normalizedVersionPolicy = NormalizeVersionPolicyConfig(versionPolicy);
        var normalizedUpdateInfo = NormalizeUpdateInfoConfig(updateInfo);
        var normalizedAccessPolicy = NormalizeAccessPolicy(accessPolicy);
        var matchedVersionPolicy = SelectVersionPolicyEntry(normalizedVersionPolicy, version, channel).MatchedPolicy;
        var matchedUpdateInfo = ResolveUpdateInfo(normalizedUpdateInfo).MatchedUpdateInfo;
        var normalizedIndex = NormalizeNoticeIndex(indexPayload);
        var normalizedManifestEntry = NormalizeManifestEntry(manifestEntry);

        var feedItems = new List<Dictionary<string, object?>>();
        var bundleSummaries = new List<Dictionary<string, object?>>();

        for (var bundleOrder = 0; bundleOrder < bundles.Count; bundleOrder++)
        {
            var record = bundles[bundleOrder];
            var entry = NormalizeBundleEntry(record.Entry);
            var bundle = NormalizeNoticeBundle(record.Bundle);
            var bundleId = TrimTo(entry.GetValueOrDefault("bundle_id")?.ToString() ?? bundle.GetValueOrDefault("bundle_id")?.ToString() ?? string.Empty, 128);
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                continue;
            }

            var visibleItems = new List<Dictionary<string, object?>>();
            var bundleItems = GetList(bundle.GetValueOrDefault("items")).OfType<Dictionary<string, object?>>().ToArray();
            for (var itemOrder = 0; itemOrder < bundleItems.Length; itemOrder++)
            {
                var item = NormalizeNoticeItem(bundleItems[itemOrder]);
                var itemId = item.GetValueOrDefault("id")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                var revisionMarker = TrimTo(
                    (item.GetValueOrDefault("updated_at")
                        ?? item.GetValueOrDefault("revision")
                        ?? bundle.GetValueOrDefault("updated_at")
                        ?? string.Empty).ToString()?.Trim() ?? string.Empty,
                    128);
                var deliveryKey = string.IsNullOrWhiteSpace(revisionMarker)
                    ? $"{bundleId}:{itemId}"
                    : $"{bundleId}:{itemId}@{revisionMarker}";

                var visible = CloneDictionary(item);
                visible["id"] = deliveryKey;
                visible["entry_id"] = itemId;
                visible["delivery_key"] = deliveryKey;
                visible["bundle_id"] = bundleId;
                visible["bundle_title"] = bundle.GetValueOrDefault("title") ?? string.Empty;
                visible["bundle_order"] = bundleOrder;
                visible["item_order"] = itemOrder;
                visible["bundle_updated_at"] = TrimTo((entry.GetValueOrDefault("updated_at") ?? bundle.GetValueOrDefault("updated_at") ?? string.Empty).ToString() ?? string.Empty, 64);
                visibleItems.Add(visible);
            }

            var summary = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = bundleId,
                ["title"] = TrimTo(bundle.GetValueOrDefault("title")?.ToString() ?? string.Empty, 128),
                ["summary"] = TrimTo(bundle.GetValueOrDefault("summary")?.ToString() ?? string.Empty, 512),
                ["updated_at"] = TrimTo((entry.GetValueOrDefault("updated_at") ?? bundle.GetValueOrDefault("updated_at") ?? string.Empty).ToString() ?? string.Empty, 64),
                ["path"] = entry.GetValueOrDefault("path") ?? string.Empty,
                ["order"] = bundleOrder,
                ["item_count"] = visibleItems.Count,
            };
            bundleSummaries.Add(summary);
            feedItems.AddRange(visibleItems);
        }

        feedItems = feedItems
            .OrderBy(item => BoolFromKeys(item, "pinned") ? 0 : 1)
            .ThenByDescending(item => ToTimestamp(item.GetValueOrDefault("updated_at") ?? item.GetValueOrDefault("bundle_updated_at")))
            .ThenBy(item => SafeInt(item.GetValueOrDefault("bundle_order"), 0))
            .ThenBy(item => SafeInt(item.GetValueOrDefault("item_order"), 0))
            .ThenBy(item => item.GetValueOrDefault("title")?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        bundleSummaries = bundleSummaries
            .OrderBy(bundle => SafeInt(bundle.GetValueOrDefault("order"), 0))
            .ThenByDescending(bundle => ToTimestamp(bundle.GetValueOrDefault("updated_at")))
            .ThenBy(bundle => bundle.GetValueOrDefault("title")?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var mergedConfig = MergeRemoteConfig(
            normalizedManifest,
            normalizedVersionPolicy,
            normalizedUpdateInfo,
            normalizedAccessPolicy,
            matchedVersionPolicy,
            matchedUpdateInfo,
            normalizedIndex,
            normalizedManifestEntry,
            bundleSummaries);

        var accessDecision = ResolveAccessPolicyDecision(normalizedAccessPolicy, studentId);
        if (BoolFromKeys(accessDecision, "blocked"))
        {
            mergedConfig["app_control"] = new Dictionary<string, object?>(NormalizeAppControl(mergedConfig.GetValueOrDefault("app_control")))
            {
                ["state"] = "blocked",
                ["reason"] = "access-policy-blocked",
            };
            mergedConfig["status_notice"] = NormalizeStatusNotice(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = accessDecision.GetValueOrDefault("message") ?? DefaultAccessPolicyMessage,
                },
                mergedConfig.GetValueOrDefault("lifecycle"),
                mergedConfig.GetValueOrDefault("app_control"));
        }

        mergedConfig["access_policy"] = normalizedAccessPolicy;
        mergedConfig["access_decision"] = accessDecision;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = string.IsNullOrWhiteSpace(channel) ? "local-cache" : source,
            ["channel"] = string.IsNullOrWhiteSpace(channel) ? "beta" : channel.Trim().ToLowerInvariant(),
            ["version"] = version?.Trim() ?? string.Empty,
            ["config"] = mergedConfig,
            ["notice"] = CloneDictionary(GetDictionary(mergedConfig.GetValueOrDefault("notice"))),
            ["notice_manifest"] = CloneDictionary(GetDictionary(mergedConfig.GetValueOrDefault("notice_manifest"))),
            ["version_policy_match"] = CloneDictionary(matchedVersionPolicy),
            ["version_policy"] = CloneDictionary(matchedVersionPolicy),
            ["version_policy_catalog"] = CloneDictionary(normalizedVersionPolicy),
            ["update_info"] = CloneDictionary(matchedUpdateInfo),
            ["update_info_catalog"] = CloneDictionary(normalizedUpdateInfo),
            ["access_policy"] = CloneDictionary(normalizedAccessPolicy),
            ["access_decision"] = CloneDictionary(accessDecision),
            ["app_policy"] = CloneDictionary(matchedVersionPolicy),
            ["software_policy"] = CloneDictionary(matchedVersionPolicy),
            ["release_policy"] = CloneDictionary(matchedUpdateInfo),
            ["release"] = CloneDictionary(matchedUpdateInfo),
            ["bundles"] = bundleSummaries.Cast<object?>().ToList(),
            ["panel_items"] = feedItems.Where(item => !string.Equals(item.GetValueOrDefault("display_mode")?.ToString(), "reminder", StringComparison.Ordinal)).Cast<object?>().ToList(),
            ["reminder_items"] = feedItems.Where(item => string.Equals(item.GetValueOrDefault("display_mode")?.ToString(), "reminder", StringComparison.Ordinal)).Cast<object?>().ToList(),
            ["items"] = feedItems.Cast<object?>().ToList(),
        };
    }

    public static bool IsSecurityWarning(string? message)
    {
        var text = (message ?? string.Empty).Trim();
        return text.Contains("验签失败", StringComparison.Ordinal)
            || text.Contains("签名缺失", StringComparison.Ordinal)
            || text.Contains("签名读取失败", StringComparison.Ordinal)
            || text.Contains("签名校验失败", StringComparison.Ordinal)
            || text.Contains("哈希缺失", StringComparison.Ordinal)
            || text.Contains("校验失败", StringComparison.Ordinal)
            || text.Contains("DPAPI", StringComparison.OrdinalIgnoreCase)
            || text.Contains("解密失败", StringComparison.Ordinal)
            || text.Contains("读取失败", StringComparison.Ordinal)
            || text.Contains("缺失", StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> UniqueWarnings(params IEnumerable<string>[] groups)
    {
        var merged = new List<string>();
        foreach (var group in groups)
        {
            foreach (var item in group)
            {
                var cleaned = (item ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned) && !merged.Contains(cleaned, StringComparer.Ordinal))
                {
                    merged.Add(cleaned);
                }
            }
        }

        return merged;
    }

    public static byte[] NoticeTextBytes(string text)
    {
        return Encoding.UTF8.GetBytes((text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n'));
    }

    public static int ResolveConfigPullIntervalSeconds(Dictionary<string, object?> config)
    {
        var runtimePolicy = GetDictionary(config.GetValueOrDefault("runtime_policy"));
        return Math.Max(300, SafeInt(runtimePolicy.GetValueOrDefault("config_pull_interval_sec"), 21600));
    }

    public static Dictionary<string, object?> BuildBlockedDetail(string studentId, Dictionary<string, object?> decision)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["blocked"] = true,
            ["message"] = TrimTo((decision.GetValueOrDefault("message") ?? DefaultAccessPolicyMessage).ToString() ?? DefaultAccessPolicyMessage, 512),
            ["student_id"] = (studentId ?? string.Empty).Trim(),
            ["student_id_hash"] = TrimTo(decision.GetValueOrDefault("student_id_hash")?.ToString() ?? string.Empty, 64),
            ["reason"] = TrimTo((decision.GetValueOrDefault("reason") ?? "access-policy-blocked").ToString() ?? "access-policy-blocked", 64),
        };
    }

    public static (Dictionary<string, object?> MatchedPolicy, Dictionary<string, object?> Catalog) SelectVersionPolicyEntry(
        Dictionary<string, object?> versionPolicy,
        string version,
        string channel)
    {
        var normalized = NormalizeVersionPolicyConfig(versionPolicy);
        var versionText = (version ?? string.Empty).Trim();
        var channelText = (channel ?? string.Empty).Trim().ToLowerInvariant();
        var fallbackByChannel = new Dictionary<string, object?>(StringComparer.Ordinal);
        var wildcard = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var rawItem in GetList(normalized.GetValueOrDefault("versions")).OfType<Dictionary<string, object?>>())
        {
            var versionId = rawItem.GetValueOrDefault("id")?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            var versionValue = rawItem.GetValueOrDefault("version")?.ToString()?.Trim() ?? string.Empty;
            if (versionValue == "*")
            {
                wildcard = rawItem;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(versionText)
                && !string.IsNullOrWhiteSpace(versionValue)
                && string.Equals(versionValue, versionText, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(versionId) || string.Equals(versionId, channelText, StringComparison.Ordinal)))
            {
                return (rawItem, normalized);
            }

            if (!string.IsNullOrWhiteSpace(versionId) && string.Equals(versionId, channelText, StringComparison.Ordinal))
            {
                fallbackByChannel = rawItem;
            }
        }

        if (fallbackByChannel.Count > 0)
        {
            return (fallbackByChannel, normalized);
        }

        return (wildcard, normalized);
    }

    public static (Dictionary<string, object?> MatchedUpdateInfo, Dictionary<string, object?> Catalog) ResolveUpdateInfo(Dictionary<string, object?> updateInfo)
    {
        var normalized = NormalizeUpdateInfoConfig(updateInfo);
        return (normalized, normalized);
    }

    public static Dictionary<string, object?> FailClosedVersionPolicy()
    {
        return NormalizeVersionPolicyConfig(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["versions"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = string.Empty,
                    ["version"] = "*",
                    ["enabled"] = true,
                    ["lifecycle"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["stage"] = "maintenance",
                    },
                    ["app_control"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["state"] = "readonly",
                        ["reason"] = "notice-integrity-check-failed",
                    },
                    ["feature_flags"] = CloneDictionary(DefaultFeatureFlags),
                    ["feature_limits"] = CloneDictionary(DefaultFeatureLimits),
                    ["runtime_policy"] = CloneDictionary(DefaultRuntimePolicy),
                },
            },
        });
    }

    public static Dictionary<string, object?> NormalizeManifestEntry(Dictionary<string, object?> rawEntry)
    {
        var manifestId = TextFromKeys(rawEntry, "id", "manifest_id", "channel");
        if (string.IsNullOrWhiteSpace(manifestId))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = TrimTo(manifestId.ToLowerInvariant(), 128),
            ["path"] = LegacyNoticePaths.NormalizeRelativePath(rawEntry.GetValueOrDefault("path")?.ToString()),
            ["updated_at"] = NormalizeIsoText(rawEntry.GetValueOrDefault("updated_at")),
            ["sha256"] = NormalizeSha(rawEntry.GetValueOrDefault("sha256")),
        };
    }

    public static Dictionary<string, object?> NormalizeBundleEntry(Dictionary<string, object?> rawEntry)
    {
        var bundleId = TextFromKeys(rawEntry, "bundle_id", "id", "key", "slug");
        var path = LegacyNoticePaths.NormalizeRelativePath(rawEntry.GetValueOrDefault("path")?.ToString());
        if (string.IsNullOrWhiteSpace(bundleId) && !string.IsNullOrWhiteSpace(path))
        {
            bundleId = Path.GetFileNameWithoutExtension(path);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["bundle_id"] = TrimTo(bundleId, 128),
            ["path"] = path,
            ["updated_at"] = NormalizeIsoText(rawEntry.GetValueOrDefault("updated_at")),
            ["sha256"] = NormalizeSha(rawEntry.GetValueOrDefault("sha256")),
        };
    }

    public static Dictionary<string, object?> NormalizeNoticeItem(Dictionary<string, object?> rawItem)
    {
        var kind = TextFromKeys(rawItem, "kind", "type", "category").ToLowerInvariant();
        if (kind is not "announcement" and not "maintenance" and not "update" and not "operation")
        {
            kind = string.Empty;
        }

        var displayMode = TextFromKeys(rawItem, "display_mode", "displayMode", "mode").ToLowerInvariant();
        displayMode = displayMode switch
        {
            "toast" => "reminder",
            "dialog" => "modal",
            "" => "panel",
            _ => displayMode,
        };
        if (displayMode is not "panel" and not "reminder" and not "modal")
        {
            displayMode = "panel";
        }

        var severity = TextFromKeys(rawItem, "severity", "level", "importance").ToLowerInvariant();
        severity = severity switch
        {
            "warn" or "warning" or "medium" or "high" => "important",
            "error" or "fatal" or "urgent" => "critical",
            "info" or "important" or "critical" or "" => severity,
            _ => string.Empty,
        };

        var contentPath = TrimTo(TextFromKeys(rawItem, "content_path", "contentPath", "body_path", "bodyPath"), 512);
        var contentMarkdown = TrimTo(TextFromKeys(rawItem, "content_markdown", "contentMarkdown", "markdown"), 50000);
        var content = TrimTo(TextFromKeys(rawItem, "content", "body", "message", "text", "detail"), 50000);
        if (!string.IsNullOrWhiteSpace(contentMarkdown) && string.IsNullOrWhiteSpace(content))
        {
            content = contentMarkdown;
        }

        var contentFormat = !string.IsNullOrWhiteSpace(contentMarkdown) || !string.IsNullOrWhiteSpace(contentPath)
            ? "markdown"
            : "text";

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = TrimTo(TextFromKeys(rawItem, "id", "item_id", "slug"), 128),
            ["title"] = TrimTo(TextFromKeys(rawItem, "title", "name", "headline"), 128),
            ["summary"] = TrimTo(TextFromKeys(rawItem, "summary", "subtitle", "description", "brief"), 512),
            ["content"] = content,
            ["content_markdown"] = contentMarkdown,
            ["content_path"] = contentPath,
            ["content_sha256"] = NormalizeSha(TextFromKeys(rawItem, "content_sha256", "contentSha256", "markdown_sha256", "body_sha256")),
            ["content_format"] = contentFormat,
            ["content_resolved_path"] = TrimTo(TextFromKeys(rawItem, "content_resolved_path", "contentResolvedPath"), 512),
            ["kind"] = kind,
            ["severity"] = severity,
            ["display_mode"] = displayMode,
            ["pinned"] = BoolFromKeys(rawItem, "pinned", "is_pinned", "sticky", "top"),
            ["rank"] = Math.Max(0, SafeInt(rawItem.GetValueOrDefault("rank"), 1000)),
            ["updated_at"] = NormalizeIsoText(rawItem.GetValueOrDefault("updated_at") ?? rawItem.GetValueOrDefault("content_updated_at")),
            ["revision"] = TrimTo(TextFromKeys(rawItem, "revision", "rev", "version"), 128),
        };
    }

    public static Dictionary<string, object?> NormalizeAppControl(object? rawAppControl)
    {
        return NormalizeAppControl(GetDictionary(rawAppControl));
    }

    public static Dictionary<string, object?> NormalizeStatusNotice(object? rawNotice, object? rawLifecycle = null, object? rawAppControl = null)
    {
        return NormalizeStatusNotice(GetDictionary(rawNotice), rawLifecycle, rawAppControl);
    }

    private static Dictionary<string, object?> NormalizeVersionPolicyEntry(Dictionary<string, object?> rawEntry)
    {
        var versionId = TrimTo((rawEntry.GetValueOrDefault("id") ?? rawEntry.GetValueOrDefault("channel") ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty, 64);
        var version = TrimTo((rawEntry.GetValueOrDefault("version") ?? rawEntry.GetValueOrDefault("build") ?? string.Empty).ToString()?.Trim() ?? string.Empty, 64);
        if (string.IsNullOrWhiteSpace(versionId) && string.IsNullOrWhiteSpace(version))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var lifecycle = NormalizeLifecycle(GetDictionary(rawEntry.GetValueOrDefault("lifecycle")));
        var appControl = NormalizeAppControl(GetDictionary(rawEntry.GetValueOrDefault("app_control")));
        if (string.Equals(GetDictionary(rawEntry.GetValueOrDefault("lifecycle")).GetValueOrDefault("stage")?.ToString()?.Trim().ToLowerInvariant(), "blocked", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(appControl.GetValueOrDefault("state")?.ToString()))
        {
            appControl["state"] = "blocked";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = versionId,
            ["version"] = version,
            ["enabled"] = rawEntry.GetValueOrDefault("enabled") is bool flag ? flag : null,
            ["updated_at"] = NormalizeIsoText(rawEntry.GetValueOrDefault("updated_at")),
            ["lifecycle"] = lifecycle,
            ["app_control"] = appControl,
            ["feature_flags"] = NormalizeFeatureFlags(GetDictionary(rawEntry.GetValueOrDefault("feature_flags"))),
            ["feature_limits"] = NormalizeFeatureLimits(GetDictionary(rawEntry.GetValueOrDefault("feature_limits"))),
            ["runtime_policy"] = NormalizeRuntimePolicy(GetDictionary(rawEntry.GetValueOrDefault("runtime_policy"))),
            ["telemetry_enabled"] = rawEntry.GetValueOrDefault("telemetry_enabled") is bool telemetry ? telemetry : null,
        };
    }

    private static Dictionary<string, object?> NormalizeFeatureFlags(Dictionary<string, object?> rawFlags)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in DefaultFeatureFlags)
        {
            if (rawFlags.TryGetValue(pair.Key, out var rawValue) && rawValue is bool boolValue)
            {
                normalized[pair.Key] = boolValue;
            }
        }

        return normalized;
    }

    private static Dictionary<string, object?> NormalizeFeatureLimits(Dictionary<string, object?> rawLimits)
    {
        var courseGrab = GetDictionary(rawLimits.GetValueOrDefault("course_grab"));
        var normalizedCourseGrab = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (courseGrab.TryGetValue("min_interval_sec", out var minIntervalRaw))
        {
            var minInterval = SafeDouble(minIntervalRaw, 0d);
            if (minInterval >= 0.1d)
            {
                normalizedCourseGrab["min_interval_sec"] = Math.Round(minInterval, 3);
            }
        }

        if (courseGrab.TryGetValue("max_runtime_sec", out var maxRuntimeRaw))
        {
            var maxRuntime = SafeInt(maxRuntimeRaw, 0);
            if (maxRuntime >= 1)
            {
                normalizedCourseGrab["max_runtime_sec"] = maxRuntime;
            }
        }

        if (courseGrab.TryGetValue("cooldown_sec", out var cooldownRaw))
        {
            var cooldown = SafeInt(cooldownRaw, -1);
            if (cooldown >= 0)
            {
                normalizedCourseGrab["cooldown_sec"] = cooldown;
            }
        }

        return normalizedCourseGrab.Count == 0
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["course_grab"] = normalizedCourseGrab,
            };
    }

    private static Dictionary<string, object?> NormalizeRuntimePolicy(Dictionary<string, object?> runtimeRaw)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (runtimeRaw.TryGetValue("config_pull_interval_sec", out var configPullRaw) && configPullRaw is not null && !string.IsNullOrWhiteSpace(configPullRaw.ToString()))
        {
            normalized["config_pull_interval_sec"] = Math.Max(300, SafeInt(configPullRaw, 21600));
        }
        if (runtimeRaw.TryGetValue("login_sync_cooldown_sec", out var loginSyncRaw) && loginSyncRaw is not null && !string.IsNullOrWhiteSpace(loginSyncRaw.ToString()))
        {
            normalized["login_sync_cooldown_sec"] = Math.Max(60, SafeInt(loginSyncRaw, 300));
        }
        if (runtimeRaw.TryGetValue("summary_report_interval_sec", out var summaryRaw) && summaryRaw is not null && !string.IsNullOrWhiteSpace(summaryRaw.ToString()))
        {
            normalized["summary_report_interval_sec"] = Math.Max(300, SafeInt(summaryRaw, 3600));
        }
        return normalized;
    }

    private static Dictionary<string, object?> NormalizeLifecycle(Dictionary<string, object?> rawLifecycle)
    {
        var stage = (rawLifecycle.GetValueOrDefault("stage")?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(stage) && !LifecycleStages.Contains(stage))
        {
            stage = string.Empty;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stage"] = stage,
            ["effective_at"] = NormalizeIsoText(rawLifecycle.GetValueOrDefault("effective_at")),
        };
    }

    private static Dictionary<string, object?> NormalizeAppControl(Dictionary<string, object?> rawAppControl)
    {
        var state = (rawAppControl.GetValueOrDefault("state")?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(state) && !AppControlStates.Contains(state))
        {
            state = string.Empty;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = state,
            ["reason"] = TrimTo(rawAppControl.GetValueOrDefault("reason")?.ToString() ?? string.Empty, 64),
        };
    }

    private static Dictionary<string, object?> NormalizeStatusNotice(Dictionary<string, object?> rawNotice, object? rawLifecycle = null, object? rawAppControl = null)
    {
        var lifecycle = GetDictionary(rawLifecycle);
        var appControl = GetDictionary(rawAppControl);
        var message =
            rawNotice.GetValueOrDefault("message")
            ?? rawNotice.GetValueOrDefault("title")
            ?? appControl.GetValueOrDefault("message")
            ?? appControl.GetValueOrDefault("title")
            ?? lifecycle.GetValueOrDefault("message")
            ?? lifecycle.GetValueOrDefault("title")
            ?? string.Empty;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = TrimTo(message?.ToString() ?? string.Empty, 20000),
        };
    }

    private static Dictionary<string, object?> MergeRemoteConfig(
        Dictionary<string, object?> noticeManifest,
        Dictionary<string, object?> versionPolicyCatalog,
        Dictionary<string, object?> updateInfoCatalog,
        Dictionary<string, object?> accessPolicyCatalog,
        Dictionary<string, object?> matchedVersionPolicy,
        Dictionary<string, object?> matchedUpdateInfo,
        Dictionary<string, object?> indexPayload,
        Dictionary<string, object?> manifestEntry,
        List<Dictionary<string, object?>> bundleSummaries)
    {
        var merged = CreateDefaultNoticeConfig();
        var noticeMeta = GetDictionary(indexPayload.GetValueOrDefault("notice"));
        var versionPolicyMeta = GetDictionary(indexPayload.GetValueOrDefault("version_policy"));
        var updateInfoMeta = GetDictionary(indexPayload.GetValueOrDefault("update_info"));
        var accessPolicyMeta = GetDictionary(indexPayload.GetValueOrDefault("access_policy"));

        merged["schema_version"] = (indexPayload.GetValueOrDefault("schema_version") ?? string.Empty).ToString()?.Trim() ?? string.Empty;
        merged["updated_at"] = (indexPayload.GetValueOrDefault("updated_at") ?? string.Empty).ToString()?.Trim() ?? string.Empty;
        merged["title"] = TrimTo(noticeManifest.GetValueOrDefault("title")?.ToString() ?? string.Empty, 64);
        merged["subtitle"] = TrimTo(noticeManifest.GetValueOrDefault("subtitle")?.ToString() ?? string.Empty, 512);
        merged["notice_text"] = TrimTo(noticeManifest.GetValueOrDefault("notice_text")?.ToString() ?? string.Empty, 20000);
        merged["config_path"] = noticeMeta.GetValueOrDefault("path") ?? string.Empty;
        merged["sha256"] = noticeMeta.GetValueOrDefault("sha256") ?? string.Empty;
        merged["notice_path"] = noticeMeta.GetValueOrDefault("path") ?? string.Empty;
        merged["notice_updated_at"] = noticeMeta.GetValueOrDefault("updated_at") ?? string.Empty;
        merged["notice_sha256"] = noticeMeta.GetValueOrDefault("sha256") ?? string.Empty;
        merged["manifest_id"] = manifestEntry.GetValueOrDefault("id") ?? string.Empty;
        merged["manifest_path"] = manifestEntry.GetValueOrDefault("path") ?? string.Empty;
        merged["manifest_updated_at"] = manifestEntry.GetValueOrDefault("updated_at") ?? string.Empty;
        merged["manifest_sha256"] = manifestEntry.GetValueOrDefault("sha256") ?? string.Empty;
        merged["version_policy_path"] = versionPolicyMeta.GetValueOrDefault("path") ?? string.Empty;
        merged["version_policy_updated_at"] = versionPolicyMeta.GetValueOrDefault("updated_at") ?? string.Empty;
        merged["version_policy_sha256"] = versionPolicyMeta.GetValueOrDefault("sha256") ?? string.Empty;
        merged["update_info_path"] = updateInfoMeta.GetValueOrDefault("path") ?? string.Empty;
        merged["update_info_updated_at"] = updateInfoMeta.GetValueOrDefault("updated_at") ?? string.Empty;
        merged["update_info_sha256"] = updateInfoMeta.GetValueOrDefault("sha256") ?? string.Empty;
        merged["access_policy_path"] = accessPolicyMeta.GetValueOrDefault("path") ?? string.Empty;
        merged["access_policy_updated_at"] = accessPolicyMeta.GetValueOrDefault("updated_at") ?? string.Empty;
        merged["access_policy_sha256"] = accessPolicyMeta.GetValueOrDefault("sha256") ?? string.Empty;
        merged["bundle_count"] = bundleSummaries.Count;
        merged["bundle_ids"] = bundleSummaries.Select(bundle => bundle.GetValueOrDefault("id") ?? string.Empty).Cast<object?>().ToList();
        merged["latest_version"] = matchedUpdateInfo.GetValueOrDefault("latest_version") ?? string.Empty;
        merged["min_supported_version"] = matchedUpdateInfo.GetValueOrDefault("min_supported_version") ?? string.Empty;
        merged["download_url"] = matchedUpdateInfo.GetValueOrDefault("download_url") ?? string.Empty;
        merged["feature_flags"] = NormalizeFeatureFlags(GetDictionary(matchedVersionPolicy.GetValueOrDefault("feature_flags")));
        merged["feature_limits"] = NormalizeFeatureLimits(GetDictionary(matchedVersionPolicy.GetValueOrDefault("feature_limits")));
        merged["runtime_policy"] = NormalizeRuntimePolicy(GetDictionary(matchedVersionPolicy.GetValueOrDefault("runtime_policy")));

        var lifecycle = NormalizeLifecycle(GetDictionary(matchedVersionPolicy.GetValueOrDefault("lifecycle")));
        var appControl = NormalizeAppControl(GetDictionary(matchedVersionPolicy.GetValueOrDefault("app_control")));
        if (matchedVersionPolicy.GetValueOrDefault("enabled") is false && string.IsNullOrWhiteSpace(appControl.GetValueOrDefault("state")?.ToString()))
        {
            appControl["state"] = "blocked";
        }

        var statusNotice = NormalizeStatusNotice(
            GetDictionary(matchedVersionPolicy.GetValueOrDefault("status_notice")),
            matchedVersionPolicy.GetValueOrDefault("lifecycle"),
            matchedVersionPolicy.GetValueOrDefault("app_control"));

        var runtimePolicy = GetDictionary(merged.GetValueOrDefault("runtime_policy"));
        merged["telemetry"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enabled"] = matchedVersionPolicy.GetValueOrDefault("telemetry_enabled") is not false,
            ["config_pull_interval_sec"] = runtimePolicy.GetValueOrDefault("config_pull_interval_sec") ?? DefaultRuntimePolicy["config_pull_interval_sec"],
            ["summary_report_interval_sec"] = runtimePolicy.GetValueOrDefault("summary_report_interval_sec") ?? DefaultRuntimePolicy["summary_report_interval_sec"],
        };
        merged["lifecycle"] = lifecycle;
        merged["app_control"] = appControl;
        merged["status_notice"] = statusNotice;
        merged["notice"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = merged.GetValueOrDefault("title") ?? string.Empty,
            ["subtitle"] = merged.GetValueOrDefault("subtitle") ?? string.Empty,
            ["notice_text"] = merged.GetValueOrDefault("notice_text") ?? string.Empty,
            ["bundles"] = bundleSummaries.Cast<object?>().ToList(),
        };
        merged["notice_manifest"] = new Dictionary<string, object?>(noticeManifest, StringComparer.Ordinal)
        {
            ["bundles"] = bundleSummaries.Cast<object?>().ToList(),
        };
        merged["version_policy_match"] = CloneDictionary(matchedVersionPolicy);
        merged["version_policy"] = CloneDictionary(matchedVersionPolicy);
        merged["update_info"] = CloneDictionary(matchedUpdateInfo);
        merged["access_policy"] = CloneDictionary(accessPolicyCatalog);
        merged["software_policy"] = CloneDictionary(matchedVersionPolicy);
        merged["app_policy"] = CloneDictionary(matchedVersionPolicy);
        merged["release_policy"] = CloneDictionary(matchedUpdateInfo);
        merged["release"] = CloneDictionary(matchedUpdateInfo);

        _ = versionPolicyCatalog;
        _ = updateInfoCatalog;

        return merged;
    }

    private static Dictionary<string, object?> LegacyAppPolicyToVersionPolicy(Dictionary<string, object?> rawConfig)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = rawConfig.GetValueOrDefault("schema_version") ?? "1",
            ["updated_at"] = rawConfig.GetValueOrDefault("updated_at") ?? string.Empty,
            ["versions"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = string.Empty,
                    ["version"] = "*",
                    ["enabled"] = true,
                    ["updated_at"] = rawConfig.GetValueOrDefault("updated_at") ?? string.Empty,
                    ["lifecycle"] = CloneDictionary(GetDictionary(rawConfig.GetValueOrDefault("lifecycle"))),
                    ["app_control"] = CloneDictionary(GetDictionary(rawConfig.GetValueOrDefault("app_control"))),
                    ["feature_flags"] = CloneDictionary(GetDictionary(rawConfig.GetValueOrDefault("feature_flags"))),
                    ["feature_limits"] = CloneDictionary(GetDictionary(rawConfig.GetValueOrDefault("feature_limits"))),
                    ["runtime_policy"] = CloneDictionary(GetDictionary(rawConfig.GetValueOrDefault("runtime_policy"))),
                    ["telemetry_enabled"] = rawConfig.GetValueOrDefault("telemetry_enabled"),
                },
            },
        };
    }

    private static Dictionary<string, object?> LegacyReleasePolicyToUpdateInfo(Dictionary<string, object?> rawConfig)
    {
        var release = GetDictionary(rawConfig.GetValueOrDefault("release"));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = rawConfig.GetValueOrDefault("schema_version") ?? "1",
            ["updated_at"] = rawConfig.GetValueOrDefault("updated_at") ?? string.Empty,
            ["latest_version"] = release.GetValueOrDefault("latest_version") ?? string.Empty,
            ["min_supported_version"] = release.GetValueOrDefault("min_supported_version") ?? string.Empty,
            ["download_url"] = release.GetValueOrDefault("download_url") ?? string.Empty,
        };
    }

    private static Dictionary<string, object?> CreateDefaultNoticeConfig()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["updated_at"] = string.Empty,
            ["title"] = string.Empty,
            ["subtitle"] = string.Empty,
            ["notice_text"] = string.Empty,
            ["config_path"] = string.Empty,
            ["sha256"] = string.Empty,
            ["notice_path"] = string.Empty,
            ["notice_updated_at"] = string.Empty,
            ["notice_sha256"] = string.Empty,
            ["manifest_id"] = string.Empty,
            ["manifest_path"] = string.Empty,
            ["manifest_updated_at"] = string.Empty,
            ["manifest_sha256"] = string.Empty,
            ["version_policy_path"] = string.Empty,
            ["version_policy_updated_at"] = string.Empty,
            ["version_policy_sha256"] = string.Empty,
            ["update_info_path"] = string.Empty,
            ["update_info_updated_at"] = string.Empty,
            ["update_info_sha256"] = string.Empty,
            ["access_policy_path"] = string.Empty,
            ["access_policy_updated_at"] = string.Empty,
            ["access_policy_sha256"] = string.Empty,
            ["bundle_count"] = 0,
            ["bundle_ids"] = new List<object?>(),
            ["latest_version"] = string.Empty,
            ["min_supported_version"] = string.Empty,
            ["download_url"] = string.Empty,
            ["feature_flags"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["feature_limits"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["telemetry"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["runtime_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["lifecycle"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["app_control"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["status_notice"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["notice"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = string.Empty,
                ["subtitle"] = string.Empty,
                ["notice_text"] = string.Empty,
                ["bundles"] = new List<object?>(),
            },
            ["notice_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["version_policy_match"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["version_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["update_info"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["access_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["access_decision"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["software_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["app_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["release_policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["release"] = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static string TextFromKeys(Dictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value))
            {
                continue;
            }

            var cleaned = (value ?? string.Empty).ToString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned;
            }
        }

        return string.Empty;
    }

    private static bool BoolFromKeys(Dictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value))
            {
                continue;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            var cleaned = (value ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            if (cleaned is "1" or "true" or "yes" or "on")
            {
                return true;
            }
            if (cleaned is "0" or "false" or "no" or "off" or "")
            {
                return false;
            }

            return value is not null;
        }

        return false;
    }

    private static int SafeInt(object? value, int fallback)
    {
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue => (int)doubleValue,
            _ when int.TryParse((value ?? string.Empty).ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static double SafeDouble(object? value, double fallback)
    {
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            decimal decimalValue => (double)decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            _ when double.TryParse((value ?? string.Empty).ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static DateTimeOffset? ParseIsoDate(object? value)
    {
        var text = (value ?? string.Empty).ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeIsoText(object? value)
    {
        return (value ?? string.Empty).ToString()?.Trim() ?? string.Empty;
    }

    private static long ToTimestamp(object? value)
    {
        var parsed = ParseIsoDate(value);
        return parsed?.ToUnixTimeSeconds() ?? 0L;
    }

    private static string NormalizeSchemaVersion(object? value, string fallback)
    {
        var text = (value ?? fallback).ToString()?.Trim() ?? fallback;
        return TrimTo(text, 32);
    }

    private static string NormalizeSha(object? value)
    {
        return TrimTo((value ?? string.Empty).ToString()?.Trim().ToLowerInvariant() ?? string.Empty, 128);
    }

    private static string TrimTo(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static Dictionary<string, object?> GetDictionary(object? value)
    {
        return value switch
        {
            Dictionary<string, object?> dict => CloneDictionary(dict),
            IReadOnlyDictionary<string, object?> readOnlyDict => readOnlyDict.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _ => new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static List<object?> GetList(object? value)
    {
        return value switch
        {
            List<object?> list => [.. list],
            IReadOnlyList<object?> readOnlyList => [.. readOnlyList],
            IEnumerable<object?> enumerable => [.. enumerable],
            _ => [],
        };
    }

    private static Dictionary<string, object?> CloneDictionary(IReadOnlyDictionary<string, object?> source)
    {
        var clone = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            clone[pair.Key] = CloneValue(pair.Value);
        }

        return clone;
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            Dictionary<string, object?> dict => CloneDictionary(dict),
            IReadOnlyDictionary<string, object?> readOnlyDict => CloneDictionary(readOnlyDict),
            List<object?> list => list.Select(CloneValue).ToList(),
            IReadOnlyList<object?> readOnlyList => readOnlyList.Select(CloneValue).ToList(),
            _ => value,
        };
    }
}
