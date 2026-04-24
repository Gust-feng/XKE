using System.Text;
using System.Text.Json;
using Xke.Security;
using Xke.Storage;

namespace Xke.Core.Notice;

public sealed class NoticeService
{
    private readonly XkeDataRoot _dataRoot;
    private readonly NoticeServiceOptions _options;
    private readonly INoticeRemoteSource _remoteSource;
    private readonly LegacyNoticeCacheReader _cacheReader;
    private readonly LegacyNoticeFeedReader _feedReader;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public NoticeService(XkeDataRoot dataRoot, NoticeServiceOptions options, INoticeRemoteSource remoteSource)
    {
        _dataRoot = dataRoot;
        _options = options;
        _remoteSource = remoteSource;
        _cacheReader = new LegacyNoticeCacheReader(_dataRoot.NoticeCache());
        _feedReader = new LegacyNoticeFeedReader(_cacheReader);
    }

    public async Task<Dictionary<string, object?>> LoadFeedAsync(
        string channel,
        string version,
        string trigger = "view",
        string studentId = "",
        CancellationToken cancellationToken = default)
    {
        var resolvedChannel = NormalizeChannel(channel);
        var resolvedTrigger = NormalizeTrigger(trigger);
        var local = ReadLocalFeed(resolvedChannel, version, studentId);
        var warnings = new List<string>();

        if (ShouldSync(local, resolvedTrigger))
        {
            warnings.AddRange(await SyncRemoteCacheAsync(resolvedChannel, cancellationToken).ConfigureAwait(false));
            local = ReadLocalFeed(resolvedChannel, version, studentId);
        }

        if (local.Feed.Count > 0)
        {
            local.Feed["warnings"] = NoticeCompatibility.UniqueWarnings(
                local.Warnings,
                warnings).Cast<object?>().ToList();

            if ((local.Feed.GetValueOrDefault("source")?.ToString() ?? string.Empty).Length == 0)
            {
                local.Feed["source"] = "local-cache";
            }

            if (NoticeCompatibility.UniqueWarnings(local.Warnings, warnings).Any(NoticeCompatibility.IsSecurityWarning))
            {
                NoticeCompatibility.ApplyFailClosedDefaults(local.Feed);
            }

            return local.Feed;
        }

        var fallbackWarnings = NoticeCompatibility.UniqueWarnings(local.Warnings, warnings, ["公告仓库尚未初始化"]);
        var fallback = NoticeCompatibility.BuildNoticeFeedPayload(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            [],
            resolvedChannel,
            version,
            NoticeCompatibility.FailClosedVersionPolicy(),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            "cos-fallback",
            studentId);
        fallback["warnings"] = fallbackWarnings.Cast<object?>().ToList();
        return NoticeCompatibility.ApplyFailClosedDefaults(fallback);
    }

    public async Task<Dictionary<string, object?>> ResolveStudentAccessDecisionAsync(
        string studentId,
        string trigger = "view",
        CancellationToken cancellationToken = default)
    {
        var normalizedStudentId = (studentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedStudentId))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["blocked"] = false,
                ["message"] = string.Empty,
                ["student_id_hash"] = string.Empty,
                ["reason"] = string.Empty,
            };
        }

        var feed = await LoadFeedAsync(_options.DefaultChannel, string.Empty, trigger, normalizedStudentId, cancellationToken).ConfigureAwait(false);
        var decision = feed.GetValueOrDefault("access_decision") as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (decision.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["blocked"] = false,
                ["message"] = string.Empty,
                ["student_id_hash"] = string.Empty,
                ["reason"] = string.Empty,
            };
        }

        if (decision.GetValueOrDefault("blocked") is true)
        {
            decision["reason"] = "access-policy-blocked";
        }

        return decision;
    }

    public Dictionary<string, object?> BuildBlockedDetail(string studentId, Dictionary<string, object?> decision)
    {
        return NoticeCompatibility.BuildBlockedDetail(studentId, decision);
    }

    private LegacyNoticeFeedResult ReadLocalFeed(string channel, string version, string studentId)
    {
        return _feedReader.ReadLocalFeed(_options.AppRoot, channel, version, studentId, _options.PublicKeyPem);
    }

    private bool ShouldSync(LegacyNoticeFeedResult localFeed, string trigger)
    {
        if (trigger is "manual" or "startup" or "login")
        {
            return true;
        }

        if (!File.Exists(_dataRoot.NoticeCache().IndexPath))
        {
            return true;
        }

        if (localFeed.Warnings.Any(NoticeCompatibility.IsSecurityWarning))
        {
            return true;
        }

        var config = localFeed.Config.Count > 0
            ? localFeed.Config
            : localFeed.Feed.GetValueOrDefault("config") as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var intervalSeconds = NoticeCompatibility.ResolveConfigPullIntervalSeconds(config);
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(_dataRoot.NoticeCache().IndexPath);
        return DateTime.UtcNow - lastWriteTimeUtc >= TimeSpan.FromSeconds(intervalSeconds);
    }

    private async Task<IReadOnlyList<string>> SyncRemoteCacheAsync(string channel, CancellationToken cancellationToken)
    {
        await _syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var warnings = new List<string>();
            var indexResult = await _remoteSource.GetBytesAsync(_options.IndexPath, cancellationToken).ConfigureAwait(false);
            if (!indexResult.Success)
            {
                warnings.Add($"云端入口读取失败：{indexResult.Error}");
                return warnings;
            }

            var signatureResult = await _remoteSource.GetBytesAsync(_options.IndexSignaturePath, cancellationToken).ConfigureAwait(false);
            if (!signatureResult.Success)
            {
                warnings.Add($"云端入口读取失败：签名读取失败：{signatureResult.Error}");
                return warnings;
            }

            Dictionary<string, object?> remoteIndex;
            try
            {
                remoteIndex = ParseJson(indexResult.Content);
            }
            catch (Exception ex)
            {
                warnings.Add($"云端入口读取失败：{ex.Message}");
                return warnings;
            }

            string publicKeyPem;
            try
            {
                publicKeyPem = string.IsNullOrWhiteSpace(_options.PublicKeyPem)
                    ? NoticePublicKeyLoader.LoadPem(_options.AppRoot)
                    : _options.PublicKeyPem;
            }
            catch (Exception ex)
            {
                warnings.Add($"云端入口读取失败：公钥读取失败：{ex.Message}");
                return warnings;
            }

            var signatureText = Encoding.UTF8.GetString(signatureResult.Content).Trim();
            if (!NoticeSignatureVerifier.VerifyIndexSignature(remoteIndex, signatureText, publicKeyPem))
            {
                warnings.Add("云端入口读取失败：签名校验失败");
                return warnings;
            }

            var normalizedIndex = NoticeCompatibility.NormalizeNoticeIndex(remoteIndex);
            var syncedMainPayloads = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            var canCommitIndex = true;

            foreach (var slot in new[]
                     {
                         ("notice", "公告目录"),
                         ("version_policy", "版本策略"),
                         ("update_info", "更新信息"),
                         ("access_policy", "访问策略"),
                     })
            {
                var entry = normalizedIndex.GetValueOrDefault(slot.Item1) as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
                var path = entry.GetValueOrDefault("path")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                {
                    syncedMainPayloads[slot.Item1] = new Dictionary<string, object?>(StringComparer.Ordinal);
                    continue;
                }

                var payload = await FetchJsonPayloadAsync(path, entry.GetValueOrDefault("sha256")?.ToString() ?? string.Empty, $"云端{slot.Item2}", warnings, cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    canCommitIndex = false;
                    continue;
                }

                syncedMainPayloads[slot.Item1] = payload;
                WriteProtectedJson(_dataRoot.NoticeCache().PayloadPath(path)!, payload);
            }

            if (canCommitIndex)
            {
                WriteProtectedJson(_dataRoot.NoticeCache().IndexPath, remoteIndex);
                WriteProtectedText(_dataRoot.NoticeCache().IndexSignaturePath, signatureText);
            }

            if (!syncedMainPayloads.TryGetValue("notice", out var noticeDirectory) || noticeDirectory.Count == 0)
            {
                return warnings;
            }

            var manifestEntry = NoticeCompatibility.SelectNoticeManifest(noticeDirectory, channel);
            var manifestPath = manifestEntry.GetValueOrDefault("path")?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return warnings;
            }

            var manifestPayload = await FetchJsonPayloadAsync(
                manifestPath,
                manifestEntry.GetValueOrDefault("sha256")?.ToString() ?? string.Empty,
                $"云端公告版本清单 {manifestEntry.GetValueOrDefault("id") ?? channel}",
                warnings,
                cancellationToken).ConfigureAwait(false);
            if (manifestPayload is null)
            {
                return warnings;
            }

            WriteProtectedJson(_dataRoot.NoticeCache().PayloadPath(manifestPath)!, manifestPayload);
            foreach (var bundleEntry in NoticeCompatibility.NormalizeNoticeCatalog(manifestPayload).GetValueOrDefault("bundles") as List<object?> ?? [])
            {
                if (bundleEntry is not Dictionary<string, object?> rawBundleEntry)
                {
                    continue;
                }

                var normalizedBundleEntry = NoticeCompatibility.NormalizeBundleEntry(rawBundleEntry);
                var bundlePath = normalizedBundleEntry.GetValueOrDefault("path")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(bundlePath))
                {
                    continue;
                }

                var bundlePayload = await FetchJsonPayloadAsync(
                    bundlePath,
                    normalizedBundleEntry.GetValueOrDefault("sha256")?.ToString() ?? string.Empty,
                    $"公告集 {normalizedBundleEntry.GetValueOrDefault("bundle_id") ?? bundlePath}",
                    warnings,
                    cancellationToken).ConfigureAwait(false);
                if (bundlePayload is null)
                {
                    continue;
                }

                WriteProtectedJson(_dataRoot.NoticeCache().PayloadPath(bundlePath)!, bundlePayload);
                var normalizedBundle = NoticeCompatibility.NormalizeNoticeBundle(bundlePayload);
                foreach (var rawItem in normalizedBundle.GetValueOrDefault("items") as List<object?> ?? [])
                {
                    if (rawItem is not Dictionary<string, object?> item)
                    {
                        continue;
                    }

                    var contentPath = item.GetValueOrDefault("content_path")?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(contentPath))
                    {
                        continue;
                    }

                    var resolvedPath = LegacyNoticePaths.ResolveRelatedRelativePath(bundlePath, contentPath);
                    if (string.IsNullOrWhiteSpace(resolvedPath)
                        || resolvedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || resolvedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var contentText = await FetchTextPayloadAsync(
                        resolvedPath,
                        item.GetValueOrDefault("content_sha256")?.ToString() ?? string.Empty,
                        $"公告正文 {resolvedPath}",
                        warnings,
                        cancellationToken).ConfigureAwait(false);
                    if (contentText is null)
                    {
                        continue;
                    }

                    WriteProtectedText(_dataRoot.NoticeCache().PayloadPath(resolvedPath)!, contentText);
                }
            }

            return warnings;
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<Dictionary<string, object?>?> FetchJsonPayloadAsync(
        string relativePath,
        string expectedSha256,
        string label,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var result = await _remoteSource.GetBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            warnings.Add($"{label}读取失败：{result.Error}");
            return null;
        }

        Dictionary<string, object?> payload;
        try
        {
            payload = ParseJson(result.Content);
        }
        catch (Exception ex)
        {
            warnings.Add($"{label}读取失败：{ex.Message}");
            return null;
        }

        var canonicalBytes = NoticeCanonicalJson.Serialize(payload);
        var actualSha = NoticeHash.Sha256Hex(canonicalBytes);
        if (!string.IsNullOrWhiteSpace(expectedSha256)
            && !string.Equals(expectedSha256.Trim().ToLowerInvariant(), actualSha, StringComparison.Ordinal))
        {
            warnings.Add($"{label}校验失败");
            return null;
        }

        return payload;
    }

    private async Task<string?> FetchTextPayloadAsync(
        string relativePath,
        string expectedSha256,
        string label,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var result = await _remoteSource.GetBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            warnings.Add($"{label}读取失败：{result.Error}");
            return null;
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(result.Content);
        }
        catch (Exception ex)
        {
            warnings.Add($"{label}读取失败：{ex.Message}");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actualSha = NoticeHash.Sha256Hex(NoticeCompatibility.NoticeTextBytes(text));
            if (!string.Equals(expectedSha256.Trim().ToLowerInvariant(), actualSha, StringComparison.Ordinal))
            {
                warnings.Add($"{label}校验失败");
                return null;
            }
        }

        return text;
    }

    private static Dictionary<string, object?> ParseJson(byte[] rawBytes)
    {
        using var document = JsonDocument.Parse(rawBytes);
        return LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
    }

    private static void WriteProtectedJson(string path, Dictionary<string, object?> payload)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, NoticeDpapi.Protect(NoticeCanonicalJson.Serialize(payload)));
    }

    private static void WriteProtectedText(string path, string text)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, NoticeDpapi.Protect(NoticeCompatibility.NoticeTextBytes(text)));
    }

    private string NormalizeChannel(string? channel)
    {
        var cleaned = (channel ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? _options.DefaultChannel : cleaned;
    }

    private static string NormalizeTrigger(string? trigger)
    {
        var cleaned = (trigger ?? string.Empty).Trim().ToLowerInvariant();
        return cleaned switch
        {
            "startup" or "login" or "runtime" or "manual" => cleaned,
            _ => "view",
        };
    }
}
