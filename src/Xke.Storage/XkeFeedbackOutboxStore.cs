using System.Text.Json;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeFeedbackOutboxStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;
    private readonly LocalEncryptedFileStore _encrypted = new();

    public XkeFeedbackOutboxStore(XkeDataRoot root)
    {
        _root = root;
    }

    public string Save(string studentId, string feedbackId, Dictionary<string, object?> payload)
    {
        // Feedback remains local-only in this slice; persisted content is encrypted at rest.
        var path = GetPath(studentId, feedbackId);
        _encrypted.WriteJson(path, payload);
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
