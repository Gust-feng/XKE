using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xke.Storage;

public sealed class XkeScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;
    private readonly XkeStudentStore _studentStore;
    private readonly Func<DateOnly> _todayProvider;

    public XkeScheduleStore(XkeDataRoot root, XkeStudentStore studentStore, Func<DateOnly>? todayProvider = null)
    {
        _root = root;
        _studentStore = studentStore;
        _todayProvider = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Now));
    }

    public string SchedulesDirectory => _studentStore.SchedulesDirectory;

    public IReadOnlyList<string> SaveSourceSchedule(
        Dictionary<string, object?> payload,
        Dictionary<string, object?> rawPayload,
        string sourceKey,
        string weekLabel)
    {
        var resolvedSourceKey = NormalizeSourceKey(sourceKey);
        var resolvedWeekLabel = NormalizeWeekLabel(weekLabel);
        var termKey = ResolveTermKey(payload);
        var weekDirectory = Path.Combine(
            SchedulesDirectory,
            XkeDataRoot.SanitizePathSegment(termKey),
            "weeks",
            XkeDataRoot.SanitizePathSegment(resolvedWeekLabel));

        Directory.CreateDirectory(weekDirectory);
        var normalizedPath = Path.Combine(weekDirectory, $"{resolvedSourceKey}.json");
        var savedFiles = new List<string> { _root.RelativePath(normalizedPath) };
        if (rawPayload.Count > 0)
        {
            var rawPath = Path.Combine(weekDirectory, $"{resolvedSourceKey}_raw.json");
            savedFiles.Add(_root.RelativePath(rawPath));
            WriteJson(rawPath, rawPayload);
        }

        payload["saved_files"] = savedFiles.Cast<object?>().ToList();
        WriteJson(normalizedPath, payload);
        return savedFiles;
    }

    public Dictionary<string, object?>? LoadLatestSourceSchedule(IReadOnlyList<string> sourceKeys)
    {
        if (!Directory.Exists(SchedulesDirectory))
        {
            return null;
        }

        var normalizedKeys = NormalizeSourceKeys(sourceKeys);
        var candidates = normalizedKeys
            .SelectMany(key => Directory.EnumerateFiles(SchedulesDirectory, $"{key}.json", SearchOption.AllDirectories))
            .Where(path => !path.EndsWith("_raw.json", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return LoadLatestPayload(candidates);
    }

    public Dictionary<string, object?>? LoadScheduleForWeek(string weekLabel, IReadOnlyList<string> sourceKeys)
    {
        if (!Directory.Exists(SchedulesDirectory))
        {
            return null;
        }

        var weekCandidates = ResolveWeekLabelCandidates(weekLabel);
        if (weekCandidates.Count == 0)
        {
            return null;
        }

        var normalizedKeys = NormalizeSourceKeys(sourceKeys);
        foreach (var sourceKey in normalizedKeys)
        {
            var candidates = weekCandidates
                .SelectMany(week => Directory.EnumerateFiles(SchedulesDirectory, $"{sourceKey}.json", SearchOption.AllDirectories)
                    .Where(path => IsWeekDirectoryMatch(path, week)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var payload = LoadLatestPayload(candidates);
            if (payload is not null)
            {
                return payload;
            }
        }

        return null;
    }

    public string FindScheduleWeekLabelForDate(DateOnly? targetDate, IReadOnlyList<string> sourceKeys)
    {
        if (!Directory.Exists(SchedulesDirectory))
        {
            return string.Empty;
        }

        var resolvedDate = targetDate ?? _todayProvider();
        var normalizedKeys = NormalizeSourceKeys(sourceKeys);
        string matchedWeekLabel = string.Empty;
        DateOnly? matchedStart = null;

        foreach (var sourceKey in normalizedKeys)
        {
            foreach (var path in Directory.EnumerateFiles(SchedulesDirectory, $"{sourceKey}.json", SearchOption.AllDirectories))
            {
                var payload = ReadJson(path);
                if (payload?.GetValueOrDefault("week_dates") is not Dictionary<string, object?> rawWeekDates)
                {
                    continue;
                }

                var dates = rawWeekDates.Values
                    .Select(value => DateOnly.TryParse(value?.ToString(), out var parsed) ? parsed : (DateOnly?)null)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .OrderBy(value => value)
                    .ToArray();
                if (dates.Length == 0)
                {
                    continue;
                }

                if (dates[0] <= resolvedDate && resolvedDate <= dates[^1])
                {
                    if (matchedStart is null || dates[0] > matchedStart.Value)
                    {
                        matchedStart = dates[0];
                        matchedWeekLabel = payload.GetValueOrDefault("week_label")?.ToString() ?? string.Empty;
                    }
                }
            }
        }

        return matchedWeekLabel;
    }

    private static Dictionary<string, object?>? LoadLatestPayload(IEnumerable<string> candidates)
    {
        Dictionary<string, object?>? latestPayload = null;
        var latestSortKey = (Timestamp: DateTimeOffset.MinValue, FileTicks: 0L);
        foreach (var path in candidates)
        {
            var payload = ReadJson(path);
            if (payload is null)
            {
                continue;
            }

            var fileInfo = new FileInfo(path);
            var sortTimestamp = ParseTimestamp(payload.GetValueOrDefault("updated_at")?.ToString())
                ?? new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
            var sortKey = (sortTimestamp, fileInfo.LastWriteTimeUtc.Ticks);
            if (latestPayload is null || sortKey.CompareTo(latestSortKey) > 0)
            {
                payload["_file"] = path;
                payload["_file_updated_at"] = fileInfo.LastWriteTimeUtc.ToString("O");
                latestPayload = payload;
                latestSortKey = sortKey;
            }
        }

        return latestPayload;
    }

    private static bool IsWeekDirectoryMatch(string path, string sanitizedWeekLabel)
    {
        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        return string.Equals(
            Path.GetFileName(parent),
            XkeDataRoot.SanitizePathSegment(sanitizedWeekLabel),
            StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ResolveWeekLabelCandidates(string weekLabel)
    {
        var raw = (weekLabel ?? string.Empty).Trim();
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            candidates.Add(raw);
        }

        var match = Regex.Match(raw, @"\d+");
        if (match.Success)
        {
            var numeric = $"第{int.Parse(match.Value)}周";
            if (!candidates.Any(value => string.Equals(value, numeric, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(numeric);
            }
        }

        return candidates;
    }

    private static IReadOnlyList<string> NormalizeSourceKeys(IReadOnlyList<string> sourceKeys)
    {
        return sourceKeys.Count == 0
            ? ["mobile_schedule", "jw_schedule"]
            : sourceKeys.Select(NormalizeSourceKey).Distinct(StringComparer.Ordinal).ToArray();
    }

    public static string NormalizeSourceKey(string source)
    {
        return (source ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "mobile" or "encrypted" or "preferred" or "mobile_schedule" => "mobile_schedule",
            "cookie" or "jw" or "jw_schedule" => "jw_schedule",
            var value when !string.IsNullOrWhiteSpace(value) => XkeDataRoot.SanitizePathSegment(value),
            _ => "mobile_schedule",
        };
    }

    public static IReadOnlyList<string> ResolveSourceKeys(string source)
    {
        return (source ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "mobile" or "encrypted" or "mobile_schedule" => ["mobile_schedule"],
            "cookie" or "jw" or "jw_schedule" => ["jw_schedule"],
            _ => ["mobile_schedule", "jw_schedule"],
        };
    }

    private static string NormalizeWeekLabel(string weekLabel)
    {
        var raw = (weekLabel ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(raw) ? "当前周" : raw;
    }

    private static string ResolveTermKey(Dictionary<string, object?> payload)
    {
        if (payload.GetValueOrDefault("term") is Dictionary<string, object?> term)
        {
            var academicTerm = term.GetValueOrDefault("academic_term")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(academicTerm))
            {
                return academicTerm;
            }

            var label = term.GetValueOrDefault("label")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }
        }

        return "current";
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static Dictionary<string, object?>? ReadJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
    }

    private static void WriteJson(string path, Dictionary<string, object?> value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }
}
