using System.Text.Json;

namespace Xke.Storage;

public static class LegacyNoticeFeedBuilder
{
    public static Dictionary<string, object?> Build(
        Dictionary<string, object?> manifest,
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
        return NoticeCompatibility.BuildNoticeFeedPayload(
            manifest,
            bundles,
            channel,
            version,
            versionPolicy,
            updateInfo,
            accessPolicy,
            indexPayload,
            manifestEntry,
            source,
            studentId);
    }

    public static Dictionary<string, object?> ApplyFailClosedDefaults(Dictionary<string, object?> feed)
    {
        return NoticeCompatibility.ApplyFailClosedDefaults(feed);
    }
}
