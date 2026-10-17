using Xke.Contracts.Responses;
using System.Globalization;
using System.Text;
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
    private static readonly string[] ScheduleCsvColumns =
    [
        "student_id", "academic_term", "week_label", "weekday", "course_name", "teacher", "room",
        "section_numbers", "week_numbers", "time_segments", "weeks", "credit", "hours_text",
    ];
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

    public SemesterScheduleData SaveSemesterSchedule(SemesterScheduleData payload)
    {
        var existing = LoadSemesterSchedule(payload.Term.AcademicTerm);
        if (existing is not null && IsNewer(existing.UpdatedAt, payload.UpdatedAt)) return existing;
        var directory = Path.Combine(_studentStore.SchedulesDirectory, XkeDataRoot.SanitizePathSegment(payload.Term.AcademicTerm), "semester");
        var json = Path.Combine(directory, "semester_schedule.json");
        var csv = Path.Combine(directory, "semester_schedule.csv");
        payload = payload with { SavedFiles = [_root.RelativePath(json), _root.RelativePath(csv)], Cached = false };
        var dictionary = JsonValueConverter.ToDictionary(JsonSerializer.SerializeToElement(payload));
        WriteScheduleCsv(csv, dictionary);
        AtomicFile.WriteText(json, JsonSerializer.Serialize(payload, JsonOptions));
        return payload;
    }

    public SemesterScheduleData? LoadSemesterSchedule(string academicTerm = "")
    {
        if (!Directory.Exists(SchedulesDirectory)) return null;
        var paths = string.IsNullOrWhiteSpace(academicTerm)
            ? Directory.EnumerateFiles(SchedulesDirectory, "semester_schedule.json", SearchOption.AllDirectories)
            : new[] { Path.Combine(SchedulesDirectory, XkeDataRoot.SanitizePathSegment(academicTerm), "semester", "semester_schedule.json") };
        SemesterScheduleData? latest = null;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            SemesterScheduleData? payload;
            try { payload = JsonSerializer.Deserialize<SemesterScheduleData>(File.ReadAllText(path), JsonOptions); }
            catch (JsonException) { continue; }
            if (payload is null || (!string.IsNullOrWhiteSpace(academicTerm) && payload.Term.AcademicTerm != academicTerm)) continue;
            // Opening a historical term later must not make it the startup semester.
            // Server term IDs are ordered YYYY-YYYY-N; capture time only compares the same term.
            var termOrder = latest is null ? 1 : string.CompareOrdinal(payload.Term.AcademicTerm, latest.Term.AcademicTerm);
            if (latest is null || termOrder > 0 || termOrder == 0 && IsNewer(payload.UpdatedAt, latest.UpdatedAt)) latest = payload;
        }
        return latest is null ? null : latest with { Cached = true };
    }

    private static bool IsNewer(string candidate, string previous)
        => DateTimeOffset.TryParse(candidate, out var next) && DateTimeOffset.TryParse(previous, out var old) && next > old;

    public IReadOnlyList<string> SaveSourceSchedule(
        Dictionary<string, object?> payload,
        Dictionary<string, object?> rawPayload,
        string sourceKey,
        string weekLabel)
    {
        var resolvedSourceKey = NormalizeSourceKey(sourceKey);
        var resolvedWeekLabel = NormalizeWeekLabel(weekLabel);
        var weekDirectoryLabel = NormalizeWeekDirectoryLabel(resolvedWeekLabel);
        var termKey = ResolveTermKey(payload);
        payload.TryAdd("student", new Dictionary<string, object?> { ["student_id"] = _studentStore.StudentId });
        var weekDirectory = Path.Combine(
            SchedulesDirectory,
            XkeDataRoot.SanitizePathSegment(termKey),
            "weeks",
            XkeDataRoot.SanitizePathSegment(weekDirectoryLabel));

        Directory.CreateDirectory(weekDirectory);
        var normalizedPath = Path.Combine(weekDirectory, $"{resolvedSourceKey}.json");
        var savedFiles = new List<string> { _root.RelativePath(normalizedPath) };
        if (rawPayload.Count > 0)
        {
            var rawPath = Path.Combine(weekDirectory, $"{resolvedSourceKey}_raw.json");
            savedFiles.Add(_root.RelativePath(rawPath));
            WriteJson(rawPath, rawPayload);
        }

        var csvPath = Path.Combine(weekDirectory, $"{resolvedSourceKey}.csv");
        savedFiles.Add(_root.RelativePath(csvPath));
        WriteScheduleCsv(csvPath, payload);

        payload["saved_files"] = savedFiles.Cast<object?>().ToList();
        WriteJson(normalizedPath, payload);
        return savedFiles;
    }

    public Dictionary<string, object?>? LoadLatestSourceSchedule(IReadOnlyList<string> sourceKeys, string academicTerm = "")
    {
        var searchRoot = string.IsNullOrWhiteSpace(academicTerm) ? SchedulesDirectory : Path.Combine(SchedulesDirectory, XkeDataRoot.SanitizePathSegment(academicTerm));
        if (!Directory.Exists(searchRoot))
        {
            return null;
        }

        var normalizedKeys = NormalizeSourceKeys(sourceKeys);
        var candidates = normalizedKeys
            .SelectMany(key => Directory.EnumerateFiles(searchRoot, $"{key}.json", SearchOption.AllDirectories))
            .Where(path => !path.EndsWith("_raw.json", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return LoadLatestPayload(candidates);
    }

    /// <summary>Newest academic term, not the historical term most recently opened.</summary>
    public Dictionary<string, object?>? LoadLatestTermSourceSchedule(IReadOnlyList<string> sourceKeys)
    {
        if (!Directory.Exists(SchedulesDirectory)) return null;
        foreach (var directory in Directory.EnumerateDirectories(SchedulesDirectory).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            var term = Path.GetFileName(directory);
            var payload = LoadLatestSourceSchedule(sourceKeys, term);
            if (payload?.GetValueOrDefault("term") is Dictionary<string, object?> metadata
                && string.Equals(metadata.GetValueOrDefault("academic_term")?.ToString(), term, StringComparison.Ordinal))
                return payload;
        }
        return null;
    }

    /// <summary>Recover only a dated anchor from this student's explicit semester, independently of course freshness.</summary>
    public DateOnly? FindSemesterStartDate(string academicTerm, IReadOnlyList<string> sourceKeys)
    {
        if (string.IsNullOrWhiteSpace(academicTerm)) return null;
        var directory = Path.Combine(SchedulesDirectory, XkeDataRoot.SanitizePathSegment(academicTerm));
        if (!Directory.Exists(directory)) return null;
        var paths = NormalizeSourceKeys(sourceKeys).SelectMany(key => Directory.EnumerateFiles(directory, $"{key}.json", SearchOption.AllDirectories));
        var calendar = LoadLatestPayload(paths, payload =>
            payload.GetValueOrDefault("term") is Dictionary<string, object?> term
            && string.Equals(term.GetValueOrDefault("academic_term")?.ToString(), academicTerm, StringComparison.Ordinal)
            && ReadSemesterStartDate(payload).HasValue);
        return calendar is null ? null : ReadSemesterStartDate(calendar);
    }

    private static DateOnly? ReadSemesterStartDate(Dictionary<string, object?> payload)
    {
        var label = payload.GetValueOrDefault("week_label")?.ToString() ?? string.Empty;
        if (DateOnly.TryParseExact(label, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return null;
        var week = ParseWeekNumber(label);
        if (week is not > 0 || payload.GetValueOrDefault("week_dates") is not Dictionary<string, object?> dates) return null;
        var monday = dates.GetValueOrDefault("星期一") ?? dates.GetValueOrDefault("周一") ?? dates.GetValueOrDefault("1");
        return DateOnly.TryParseExact(monday?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date.DayOfWeek == DayOfWeek.Monday ? date.AddDays(-(week.Value - 1) * 7) : null;
    }

    public Dictionary<string, object?>? LoadScheduleForWeek(string weekLabel, IReadOnlyList<string> sourceKeys, string academicTerm = "")
    {
        var searchRoot = string.IsNullOrWhiteSpace(academicTerm) ? SchedulesDirectory : Path.Combine(SchedulesDirectory, XkeDataRoot.SanitizePathSegment(academicTerm));
        if (!Directory.Exists(searchRoot))
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
                .SelectMany(week => Directory.EnumerateFiles(searchRoot, $"{sourceKey}.json", SearchOption.AllDirectories)
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

    public string FindScheduleWeekLabelForDate(DateOnly? targetDate, IReadOnlyList<string> sourceKeys, string academicTerm = "")
    {
        var searchRoot = string.IsNullOrWhiteSpace(academicTerm) ? SchedulesDirectory : Path.Combine(SchedulesDirectory, XkeDataRoot.SanitizePathSegment(academicTerm));
        if (!Directory.Exists(searchRoot))
        {
            return string.Empty;
        }

        var resolvedDate = targetDate ?? _todayProvider();
        var normalizedKeys = NormalizeSourceKeys(sourceKeys);
        string matchedWeekLabel = string.Empty;
        DateOnly? matchedStart = null;

        foreach (var sourceKey in normalizedKeys)
        {
            foreach (var path in Directory.EnumerateFiles(searchRoot, $"{sourceKey}.json", SearchOption.AllDirectories))
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

    private static Dictionary<string, object?>? LoadLatestPayload(IEnumerable<string> candidates, Func<Dictionary<string, object?>, bool>? include = null)
    {
        Dictionary<string, object?>? latestPayload = null;
        var latestSortKey = (Timestamp: DateTimeOffset.MinValue, FileTicks: 0L);
        foreach (var path in candidates)
        {
            var payload = ReadJson(path);
            if (payload is null || include is not null && !include(payload))
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
        AddWeekLabelCandidate(candidates, raw);

        var weekNumber = ParseWeekNumber(raw);
        if (weekNumber is not null)
        {
            AddWeekLabelCandidate(candidates, $"第{weekNumber.Value}周");
            AddWeekLabelCandidate(candidates, $"第{ToChineseNumber(weekNumber.Value)}周");
        }

        return candidates;
    }

    private static void AddWeekLabelCandidate(List<string> candidates, string value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        if (!candidates.Any(item => string.Equals(item, cleaned, StringComparison.OrdinalIgnoreCase)))
        {
            candidates.Add(cleaned);
        }
    }

    private static int? ParseWeekNumber(string value)
    {
        var cleaned = Regex.Replace(value ?? string.Empty, @"\s+", string.Empty);
        var match = Regex.Match(cleaned, @"\d+");
        if (match.Success)
        {
            return int.Parse(match.Value);
        }

        var chineseMatch = Regex.Match(cleaned, @"第?([零〇一二两三四五六七八九十百]+)周?");
        return chineseMatch.Success ? ParseChineseNumber(chineseMatch.Groups[1].Value) : null;
    }

    private static int? ParseChineseNumber(string value)
    {
        var total = 0;
        var current = 0;
        foreach (var ch in value ?? string.Empty)
        {
            if (TryParseChineseDigit(ch, out var digit))
            {
                current = digit;
                continue;
            }

            var unit = ch switch
            {
                '十' => 10,
                '百' => 100,
                _ => 0,
            };
            if (unit == 0)
            {
                return null;
            }

            total += (current == 0 ? 1 : current) * unit;
            current = 0;
        }

        total += current;
        return total > 0 ? total : null;
    }

    private static bool TryParseChineseDigit(char value, out int digit)
    {
        digit = value switch
        {
            '零' or '〇' => 0,
            '一' => 1,
            '二' or '两' => 2,
            '三' => 3,
            '四' => 4,
            '五' => 5,
            '六' => 6,
            '七' => 7,
            '八' => 8,
            '九' => 9,
            _ => -1,
        };
        return digit >= 0;
    }

    private static string ToChineseNumber(int value)
    {
        if (value <= 0)
        {
            return value.ToString();
        }

        string[] digits = ["零", "一", "二", "三", "四", "五", "六", "七", "八", "九"];
        if (value < 10)
        {
            return digits[value];
        }

        if (value < 20)
        {
            return value == 10 ? "十" : $"十{digits[value % 10]}";
        }

        if (value < 100)
        {
            var tens = value / 10;
            var ones = value % 10;
            return ones == 0 ? $"{digits[tens]}十" : $"{digits[tens]}十{digits[ones]}";
        }

        return value.ToString();
    }

    private static IReadOnlyList<string> NormalizeSourceKeys(IReadOnlyList<string> sourceKeys)
        => sourceKeys.Count == 0 ? ["jw_schedule"] : sourceKeys.Select(NormalizeSourceKey).Distinct(StringComparer.Ordinal).ToArray();

    public static string NormalizeSourceKey(string source)
        => (source ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "" or "preferred" or "cookie" or "jw" or "jw_schedule" => "jw_schedule",
            _ => throw new ArgumentException("不支持的课表来源", nameof(source)),
        };

    public static IReadOnlyList<string> ResolveSourceKeys(string source)
        => [NormalizeSourceKey(source)];

    private static string NormalizeWeekLabel(string weekLabel)
    {
        var raw = (weekLabel ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(raw) ? "当前周" : raw;
    }

    private static string NormalizeWeekDirectoryLabel(string weekLabel)
    {
        var weekNumber = ParseWeekNumber(weekLabel);
        return weekNumber is null ? NormalizeWeekLabel(weekLabel) : $"第{weekNumber.Value}周";
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

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return JsonValueConverter.ToDictionary(document.RootElement.Clone());
        }
        catch (JsonException) { return null; }
    }

    private static void WriteJson(string path, Dictionary<string, object?> value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void WriteScheduleCsv(string path, Dictionary<string, object?> payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", ScheduleCsvColumns.Select(EscapeCsvValue)));
        foreach (var row in EnumerateScheduleRows(payload.GetValueOrDefault("rows")))
        {
            var values = ScheduleCsvColumns.Select(column => EscapeCsvValue(CsvField(row, payload, column)));
            builder.AppendLine(string.Join(",", values));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteText(path, builder.ToString());
    }

    private static IEnumerable<Dictionary<string, object?>> EnumerateScheduleRows(object? value)
    {
        if (value is JsonElement element)
        {
            value = JsonValueConverter.ToObject(element);
        }

        if (value is not IEnumerable<object?> rows)
        {
            yield break;
        }

        foreach (var row in rows)
        {
            var dictionary = ToDictionary(row);
            if (dictionary.Count > 0)
            {
                yield return dictionary;
            }
        }
    }

    private static string CsvField(
        Dictionary<string, object?> row,
        Dictionary<string, object?> payload,
        string column)
    {
        if (column == "student_id") return CsvText(ToDictionary(payload.GetValueOrDefault("student")).GetValueOrDefault("student_id"));
        if (column == "academic_term") return ResolveTermKey(payload);
        if (row.TryGetValue(column, out var array) && array is System.Collections.IEnumerable sequence && array is not string)
            return string.Join(";", sequence.Cast<object?>().Select(CsvText));
        if (row.TryGetValue(column, out var value) && value is not null)
        {
            return CsvText(value);
        }

        return column is "week_label"
            ? CsvText(payload.GetValueOrDefault(column))
            : string.Empty;
    }

    private static Dictionary<string, object?> ToDictionary(object? value)
    {
        if (value is JsonElement element)
        {
            value = JsonValueConverter.ToObject(element);
        }

        return value switch
        {
            Dictionary<string, object?> dictionary => dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _ => [],
        };
    }

    private static string CsvText(object? value)
    {
        if (value is JsonElement element)
        {
            value = JsonValueConverter.ToObject(element);
        }

        return value switch
        {
            null => string.Empty,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string EscapeCsvValue(string value)
    {
        return value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
