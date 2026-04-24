using System.Text.Json;

namespace Xke.Storage;

public sealed class XkeFeedbackOutboxStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;

    public XkeFeedbackOutboxStore(XkeDataRoot root)
    {
        _root = root;
    }

    public string Save(string studentId, string feedbackId, Dictionary<string, object?> payload)
    {
        // Feedback stays local-only in this slice, so the outbox is a plain repo-local queue under .runtime/data.
        var path = GetPath(studentId, feedbackId);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
        return _root.RelativePath(path);
    }

    public string GetPath(string studentId, string feedbackId)
    {
        return Path.Combine(
            _root.FeedbackOutboxDirectory,
            XkeDataRoot.SanitizePathSegment(studentId),
            $"{XkeDataRoot.SanitizePathSegment(feedbackId)}.json");
    }
}
