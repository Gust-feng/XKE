namespace Xke.Storage;

public static class LegacyNoticeConfigMerger
{
    public static Dictionary<string, object?> Merge(
        Dictionary<string, object?> noticeManifest,
        Dictionary<string, object?> matchedVersionPolicy,
        Dictionary<string, object?> matchedUpdateInfo,
        Dictionary<string, object?> normalizedAccessPolicy,
        Dictionary<string, object?> normalizedIndex,
        Dictionary<string, object?> manifestEntry,
        List<object?> bundleSummaries)
    {
        var records = bundleSummaries
            .OfType<Dictionary<string, object?>>()
            .Select(item => new LegacyNoticeBundleRecord
            {
                Entry = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["bundle_id"] = item.GetValueOrDefault("id") ?? string.Empty,
                    ["path"] = item.GetValueOrDefault("path") ?? string.Empty,
                    ["updated_at"] = item.GetValueOrDefault("updated_at") ?? string.Empty,
                },
                Bundle = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["title"] = item.GetValueOrDefault("title") ?? string.Empty,
                    ["summary"] = item.GetValueOrDefault("summary") ?? string.Empty,
                },
            })
            .ToList();

        var feed = NoticeCompatibility.BuildNoticeFeedPayload(
            noticeManifest,
            records,
            string.Empty,
            string.Empty,
            matchedVersionPolicy,
            matchedUpdateInfo,
            normalizedAccessPolicy,
            normalizedIndex,
            manifestEntry,
            "local-cache",
            string.Empty);

        return feed.GetValueOrDefault("config") as Dictionary<string, object?> ?? new Dictionary<string, object?>();
    }
}
