using System.Text.Json.Serialization;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeFeedbackUsageStore
{
    public const int DefaultDailyLimit = 7;

    private static readonly object Gate = new();

    private readonly XkeDataRoot _root;
    private readonly LocalEncryptedFileStore _encrypted = new();

    public XkeFeedbackUsageStore(XkeDataRoot root)
    {
        _root = root;
    }

    public FeedbackUsageRecordResult RecordDailySubmission(
        string studentId,
        string feedbackId,
        DateTimeOffset now,
        int dailyLimit = DefaultDailyLimit)
    {
        var limit = Math.Clamp(dailyLimit, 1, 100);
        var day = now.ToString("yyyyMMdd");
        var path = GetDailyUsagePath(studentId, day);

        lock (Gate)
        {
            var document = Load(path, studentId, day, limit);
            document.Entries.RemoveAll(entry => string.IsNullOrWhiteSpace(entry.FeedbackId));
            if (document.Entries.Count >= limit)
            {
                return new FeedbackUsageRecordResult(false, document.Entries.Count, limit, day);
            }

            document.Limit = limit;
            document.Entries.Add(new FeedbackUsageEntry
            {
                FeedbackId = feedbackId,
                RecordedAt = now.ToString("O"),
            });
            _encrypted.WriteJson(path, document);
            return new FeedbackUsageRecordResult(true, document.Entries.Count, limit, day);
        }
    }

    public string GetDailyUsagePath(string studentId, string day)
    {
        return Path.Combine(
            _root.FeedbackUsageDirectory,
            XkeDataRoot.SanitizePathSegment(studentId),
            $"{XkeDataRoot.SanitizePathSegment(day)}.json");
    }

    private FeedbackUsageDocument Load(string path, string studentId, string day, int limit)
    {
        if (!File.Exists(path))
        {
            return new FeedbackUsageDocument
            {
                StudentId = studentId,
                Day = day,
                Limit = limit,
            };
        }

        try
        {
            return _encrypted.ReadJson<FeedbackUsageDocument>(path) ?? new FeedbackUsageDocument
            {
                StudentId = studentId,
                Day = day,
                Limit = limit,
            };
        }
        catch
        {
            return new FeedbackUsageDocument
            {
                StudentId = studentId,
                Day = day,
                Limit = limit,
            };
        }
    }

    private sealed class FeedbackUsageDocument
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; } = 1;

        [JsonPropertyName("student_id")]
        public string StudentId { get; init; } = string.Empty;

        [JsonPropertyName("day")]
        public string Day { get; init; } = string.Empty;

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = DefaultDailyLimit;

        [JsonPropertyName("entries")]
        public List<FeedbackUsageEntry> Entries { get; init; } = [];
    }

    private sealed class FeedbackUsageEntry
    {
        [JsonPropertyName("feedback_id")]
        public string FeedbackId { get; init; } = string.Empty;

        [JsonPropertyName("recorded_at")]
        public string RecordedAt { get; init; } = string.Empty;
    }
}

public sealed record FeedbackUsageRecordResult(
    bool Accepted,
    int Count,
    int Limit,
    string Day);
