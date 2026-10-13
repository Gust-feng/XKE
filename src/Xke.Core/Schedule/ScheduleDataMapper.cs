using System.Text.Json;
using System.Text.RegularExpressions;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Schedule;

/// <summary>Shared business projection for desktop and protocol hosts; no IO or session state.</summary>
public static class ScheduleDataMapper
{
    public static Dictionary<string, object?> FromWeek(JwScheduleFetchResult result, StudentProfileDocument profile,
        ScheduleRequest? request = null, string fallbackReason = "")
        => NormalizeWeek(BuildPayloadFromFetchResult(result), profile, false, "live_fetch", result.SourceKey,
            result.SourceLabel, result.SourceType, fallbackReason, request);

    public static SemesterScheduleData FromSemester(JwScheduleFetchResult result, StudentProfileDocument profile)
    {
        var warnings = NormalizeList(result.DataWarnings);
        var rows = NormalizeScheduleRows(result.Rows, warnings);
        T Convert<T>(object? value) => JsonSerializer.SerializeToElement(value).Deserialize<T>()!;
        return new SemesterScheduleData(Convert<ScheduleTermSummary>(NormalizeTerm(result.RawPayload.GetValueOrDefault("term"), null)),
            new ScheduleStudentSummary(profile.StudentId, profile.Name), Convert<ScheduleRow[]>(rows),
            Convert<ScheduleBlockTime[]>(result.BlockTimes),
            Convert<ScheduleTermOption[]>(result.RawPayload.GetValueOrDefault("available_terms") ?? Array.Empty<object>()),
            Convert<string[]>(result.RawPayload.GetValueOrDefault("notes") ?? Array.Empty<string>()),
            Convert<Dictionary<string, string>[]>(result.RawPayload.GetValueOrDefault("supplementary_courses") ?? Array.Empty<object>()),
            NormalizeTimestamp(result.RawPayload.GetValueOrDefault("updated_at")), []);
    }

    private static Dictionary<string, object?> BuildPayloadFromFetchResult(JwScheduleFetchResult result)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = result.SourceLabel,
            ["source_label"] = result.SourceLabel,
            ["source_key"] = result.SourceKey,
            ["source_type"] = result.SourceType,
            ["rows"] = result.Rows.Cast<object?>().ToList(),
            ["week_label"] = result.WeekLabel,
            ["current_week_label"] = ResolveCurrentWeekLabel(result),
            ["available_weeks"] = result.AvailableWeeks.Cast<object?>().ToList(),
            ["week_dates"] = result.WeekDates.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal),
            ["current_week_number"] = result.CurrentWeekNumber,
            ["viewed_week_number"] = result.ViewedWeekNumber,
            ["remarks"] = result.Remarks,
            ["block_times"] = result.BlockTimes.Cast<object?>().ToList(),
            ["fallback_reason"] = result.FallbackReason,
            ["data_warnings"] = result.DataWarnings.Cast<object?>().ToList(),
            ["notes"] = result.RawPayload.GetValueOrDefault("notes") ?? Array.Empty<string>(),
            ["supplementary_courses"] = result.RawPayload.GetValueOrDefault("supplementary_courses") ?? Array.Empty<object>(),
        };

        payload["retrieval_channel"] = result.RawPayload.GetValueOrDefault("retrieval_channel") ?? "direct_jw";
        if (result.RawPayload.TryGetValue("updated_at", out var fetchedAt)) payload["updated_at"] = fetchedAt;
        if (result.RawPayload.TryGetValue("term", out var term) && term is not null)
        {
            payload["term"] = term;
        }

        return payload;
    }

    private static string ResolveCurrentWeekLabel(JwScheduleFetchResult result)
    {
        return result.CurrentWeekNumber is int currentWeekNumber && currentWeekNumber > 0
            ? $"第{currentWeekNumber}周"
            : string.Empty;
    }

    public static Dictionary<string, object?> NormalizeWeek(
        Dictionary<string, object?> rawPayload,
        StudentProfileDocument profile,
        bool cached,
        string dataOrigin,
        string sourceKey,
        string sourceLabel,
        string sourceType,
        string fallbackReason,
        ScheduleRequest? request)
    {
        var payload = new Dictionary<string, object?>(rawPayload, StringComparer.Ordinal);
        var resolvedSourceKey = XkeScheduleStore.NormalizeSourceKey(sourceKey);
        var resolvedSourceType = NormalizeSourceType(sourceType, resolvedSourceKey);
        var resolvedSourceLabel = string.IsNullOrWhiteSpace(sourceLabel)
            ? ResolveSourceLabel(resolvedSourceKey)
            : sourceLabel.Trim();
        var weekLabel = CanonicalWeekLabel(payload.GetValueOrDefault("week_label")?.ToString());
        if (string.IsNullOrWhiteSpace(weekLabel))
        {
            weekLabel = "当前周";
        }

        var currentWeekNumber = ReadInt(payload.GetValueOrDefault("current_week_number"));
        var viewedWeekNumber = ReadInt(payload.GetValueOrDefault("viewed_week_number"));
        var currentWeekLabel = CanonicalWeekLabel(payload.GetValueOrDefault("current_week_label")?.ToString());
        if (string.IsNullOrWhiteSpace(currentWeekLabel) && currentWeekNumber is int resolvedCurrentWeekNumber && resolvedCurrentWeekNumber > 0)
        {
            currentWeekLabel = $"第{resolvedCurrentWeekNumber}周";
        }

        currentWeekNumber ??= ParseWeekNumber(currentWeekLabel);
        viewedWeekNumber ??= ParseWeekNumber(weekLabel);
        var dataWarnings = NormalizeList(payload.GetValueOrDefault("data_warnings"));

        payload["source"] = resolvedSourceLabel;
        payload["source_label"] = resolvedSourceLabel;
        payload["source_key"] = resolvedSourceKey;
        payload["source_type"] = resolvedSourceType;
        payload["rows"] = NormalizeScheduleRows(payload.GetValueOrDefault("rows"), dataWarnings);
        foreach (var row in ((List<object?>)payload["rows"]!).OfType<Dictionary<string, object?>>()) row["week_label"] = weekLabel;
        payload["week_label"] = weekLabel;
        payload["available_weeks"] = NormalizeAvailableWeeks(payload.GetValueOrDefault("available_weeks"), weekLabel);
        payload["week_dates"] = ResolveWeekDates(payload.GetValueOrDefault("week_dates"), (List<object?>)payload["available_weeks"]!, weekLabel);
        payload["current_week_label"] = currentWeekLabel;
        payload["current_week_number"] = currentWeekNumber;
        payload["viewed_week_number"] = viewedWeekNumber;
        payload["remarks"] = (payload.GetValueOrDefault("remarks")?.ToString() ?? (cached ? "当前显示本地缓存课表" : "课表刷新成功")).Trim();
        payload["block_times"] = NormalizeList(payload.GetValueOrDefault("block_times"));
        payload["fallback_reason"] = string.IsNullOrWhiteSpace(fallbackReason)
            ? (payload.GetValueOrDefault("fallback_reason")?.ToString() ?? string.Empty)
            : fallbackReason;
        payload["data_warnings"] = DeduplicateScheduleWarnings(dataWarnings);
        payload["term"] = NormalizeTerm(payload.GetValueOrDefault("term"), request);
        payload["student"] = BuildStudentPayload(profile);
        payload["cached"] = cached;
        payload["data_origin"] = dataOrigin;
        payload["retrieval_channel"] = payload.GetValueOrDefault("retrieval_channel")?.ToString() ?? "direct_jw";
        payload["updated_at"] = NormalizeTimestamp(payload.GetValueOrDefault("updated_at"));
        payload["file_updated_at"] = payload.GetValueOrDefault("_file_updated_at")?.ToString()
            ?? payload.GetValueOrDefault("file_updated_at")?.ToString()
            ?? string.Empty;

        payload["saved_files"] = NormalizeList(payload.GetValueOrDefault("saved_files"));
        return payload;
    }

    private static int? ReadInt(object? value)
    {
        return value switch
        {
            int directInt => directInt,
            long directLong when directLong >= int.MinValue && directLong <= int.MaxValue => (int)directLong,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var numberInt) => numberInt,
            JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var stringInt) => stringInt,
            _ => int.TryParse(value?.ToString(), out var parsed) ? parsed : null,
        };
    }

    private static Dictionary<string, object?> BuildStudentPayload(StudentProfileDocument profile)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["student_id"] = profile.StudentId,
            ["name"] = string.IsNullOrWhiteSpace(profile.Name) ? profile.StudentId : profile.Name,
            ["nickname"] = profile.Nickname,
            ["use_nickname"] = profile.UseNickname,
            ["class_name"] = profile.ClassName,
            ["major_name"] = profile.MajorName,
            ["academy_name"] = profile.AcademyName,
            ["training_level"] = profile.TrainingLevel,
            ["campus_name"] = profile.CampusName,
            ["updated_at"] = profile.UpdatedAt,
        };
    }

    private static List<object?> NormalizeList(object? value)
    {
        return value switch
        {
            null => [],
            List<object?> list => list,
            IEnumerable<object?> values => values.ToList(),
            System.Collections.IEnumerable values => values.Cast<object?>().ToList(),
            _ => [],
        };
    }

    private static List<object?> NormalizeScheduleRows(object? value, List<object?> dataWarnings)
    {
        var rows = NormalizeList(value);
        var normalized = new List<object?>();
        var skipped = 0;

        foreach (var item in rows)
        {
            var row = NormalizeObjectDictionary(item);
            if (row.Count == 0)
            {
                skipped++;
                continue;
            }

            var courseName = FirstText(row, "course_name", "course", "name", "title");
            var weekday = NormalizeWeekdayText(FirstText(row, "weekday", "day", "week_day"));
            var section = FirstNormalizedSectionText(row, "section", "sections", "class_time", "time_text");
            if (string.IsNullOrWhiteSpace(courseName)
                || string.IsNullOrWhiteSpace(weekday)
                || string.IsNullOrWhiteSpace(section))
            {
                skipped++;
                continue;
            }

            row["course_name"] = courseName;
            row["weekday"] = weekday;
            row["section"] = section;
            row["source"] = FirstText(row, "source");
            row["week_label"] = FirstText(row, "week_label");
            row["time_range"] = FirstText(row, "time_range");
            row["block_time"] = FirstText(row, "block_time");
            row["small_section_1"] = FirstText(row, "small_section_1");
            row["small_section_2"] = FirstText(row, "small_section_2");
            row["teacher"] = TrimPrefix(FirstText(row, "teacher", "teacher_name"), "教师：");
            row["room"] = FirstText(row, "room", "classroom", "location");
            row["weeks"] = FirstText(row, "weeks", "week", "week_info");
            row["credit"] = FirstText(row, "credit");
            if (row.GetValueOrDefault("section_numbers") is null) row["section_numbers"] = ScheduleSectionNormalizer.ExtractSectionNumbers(section);
            if (row.GetValueOrDefault("week_numbers") is null) row["week_numbers"] = ScheduleWeekPattern.Parse(row["weeks"]?.ToString() ?? "");
            if (row.GetValueOrDefault("time_segments") is null) row["time_segments"] = (row["time_range"]?.ToString() ?? "").Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!row.ContainsKey("break_minutes"))
            {
                row["break_minutes"] = 0;
            }

            var timeText = FirstText(row, "time_text");
            if (string.IsNullOrWhiteSpace(timeText))
            {
                row["time_text"] = string.Join(" / ", new[]
                {
                    weekday,
                    section,
                    row.GetValueOrDefault("time_range")?.ToString() ?? string.Empty,
                }.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            normalized.Add(row);
        }

        if (skipped > 0)
        {
            dataWarnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "schedule-row-invalid",
                ["message"] = $"已跳过 {skipped} 条缺少 weekday/section/course_name 的不可渲染课表行",
            });
        }

        return DeduplicateScheduleRows(normalized);
    }

    private static List<object?> DeduplicateScheduleRows(List<object?> rows)
    {
        string[] fields = ["weekday", "section", "course_name", "teacher", "room", "weeks", "credit",
            "time_range", "block_time", "small_section_1", "small_section_2", "break_minutes",
            "section_numbers", "week_numbers", "time_segments", "hours_text", "hours_variants"];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Different locations or week/time rules are different arrangements.
        return rows.OfType<Dictionary<string, object?>>()
            .Where(row => seen.Add(JsonSerializer.Serialize(fields.Select(field => row.GetValueOrDefault(field)))))
            .Cast<object?>().ToList();
    }

    private static List<object?> DeduplicateScheduleWarnings(List<object?> warnings)
    {
        var result = new List<object?>();
        var byKey = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var primitiveKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var warning in warnings)
        {
            var dictionary = NormalizeObjectDictionary(warning);
            if (dictionary.Count == 0)
            {
                var primitiveKey = warning?.ToString() ?? string.Empty;
                if (primitiveKeys.Add(primitiveKey))
                {
                    result.Add(warning);
                }

                continue;
            }

            var key = BuildScheduleWarningKey(dictionary);
            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = dictionary;
                result.Add(dictionary);
                continue;
            }

            MergeScheduleWarningDiscardedRooms(existing, dictionary);
        }

        return result;
    }

    private static string BuildScheduleWarningKey(Dictionary<string, object?> warning)
    {
        var type = (warning.GetValueOrDefault("type")?.ToString() ?? string.Empty).Trim();
        if (string.Equals(type, "duplicate_slot", StringComparison.Ordinal))
        {
            return string.Join(
                "\u001f",
                type,
                warning.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
                warning.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
                warning.GetValueOrDefault("section")?.ToString() ?? string.Empty,
                warning.GetValueOrDefault("kept_room")?.ToString() ?? string.Empty);
        }

        return string.Join(
            "\u001f",
            type,
            warning.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("section")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("kept_room")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("kept_weeks")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("message")?.ToString() ?? string.Empty);
    }

    private static void MergeScheduleWarningDiscardedRooms(Dictionary<string, object?> target, Dictionary<string, object?> source)
    {
        var rooms = new List<string>();
        foreach (var item in NormalizeList(target.GetValueOrDefault("discarded")).Concat(NormalizeList(source.GetValueOrDefault("discarded"))))
        {
            var room = NormalizeObjectDictionary(item).GetValueOrDefault("room")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(room) && !rooms.Contains(room, StringComparer.Ordinal))
            {
                rooms.Add(room);
            }
        }

        if (rooms.Count > 0)
        {
            target["discarded"] = rooms
                .Select(room => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["room"] = room,
                })
                .ToList();
        }
    }

    private static Dictionary<string, object?> NormalizeObjectDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is System.Collections.IDictionary dictionaryObject)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in dictionaryObject)
            {
                if (entry.Key is string key)
                {
                    result[key] = entry.Value;
                }
            }

            return result;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = JsonElementToObject(property.Value);
            }

            return result;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => NormalizeObjectDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            _ => null,
        };
    }

    private static string FirstText(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                var text = value?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static string FirstNormalizedSectionText(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
            {
                continue;
            }

            var normalized = NormalizeSectionText(value?.ToString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return string.Empty;
    }

    private static string NormalizeWeekdayText(string value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        var match = Regex.Match(cleaned, @"星期[一二三四五六日天]");
        if (match.Success)
        {
            return match.Value == "星期天" ? "星期日" : match.Value;
        }

        return cleaned switch
        {
            "周一" or "1" => "星期一",
            "周二" or "2" => "星期二",
            "周三" or "3" => "星期三",
            "周四" or "4" => "星期四",
            "周五" or "5" => "星期五",
            "周六" or "6" => "星期六",
            "周日" or "周天" or "0" or "7" => "星期日",
            _ => cleaned,
        };
    }

    private static string NormalizeSectionText(string value)
    {
        return ScheduleSectionNormalizer.NormalizeSectionText(value);
    }

    private static string TrimPrefix(string value, string prefix)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned.StartsWith(prefix, StringComparison.Ordinal)
            ? cleaned[prefix.Length..].Trim()
            : cleaned;
    }

    private static Dictionary<string, object?> NormalizeStringDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static string CanonicalWeekLabel(string? label)
    {
        var cleaned = (label ?? string.Empty).Trim();
        if (!Regex.IsMatch(cleaned, @"^(?:第\s*)?(?:\d+|[零〇一二两三四五六七八九十百]+)\s*周?$")) return cleaned;
        var number = ParseWeekNumber(cleaned);
        return number is > 0 ? $"第{number}周" : cleaned;
    }

    private static List<object?> NormalizeAvailableWeeks(object? value, string weekLabel)
    {
        var weeks = NormalizeList(value).OfType<Dictionary<string, object?>>()
            .Select(option => (object?)new Dictionary<string, object?>(option, StringComparer.Ordinal)
            {
                ["label"] = CanonicalWeekLabel(option.GetValueOrDefault("label")?.ToString()),
                ["selected"] = CanonicalWeekLabel(option.GetValueOrDefault("label")?.ToString()) == weekLabel,
            }).ToList();
        if (weeks.Count == 0 && !string.IsNullOrWhiteSpace(weekLabel))
        {
            weeks.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = ParseWeekNumber(weekLabel)?.ToString() ?? weekLabel,
                ["label"] = weekLabel,
                ["selected"] = true,
            });
        }
        return weeks;
    }

    private static Dictionary<string, object?> ResolveWeekDates(object? value, List<object?> weeks, string weekLabel)
    {
        var dates = NormalizeStringDictionary(value);
        if (dates.Count > 0) return dates;
        var option = weeks.OfType<Dictionary<string, object?>>()
            .FirstOrDefault(item => item.GetValueOrDefault("label")?.ToString() == weekLabel);
        if (!DateOnly.TryParseExact(option?.GetValueOrDefault("value")?.ToString(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var monday)
            || monday.DayOfWeek != DayOfWeek.Monday) return dates;
        string[] days = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
        for (var index = 0; index < days.Length; index++)
            dates[days[index]] = monday.AddDays(index).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return dates;
    }

    private static Dictionary<string, object?> NormalizeTerm(object? value, ScheduleRequest? request)
    {
        var term = value is Dictionary<string, object?> dictionary
            ? new Dictionary<string, object?>(dictionary, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!term.ContainsKey("label"))
        {
            term["label"] = request?.TermLabel?.Trim() ?? string.Empty;
        }

        if (!term.ContainsKey("academic_term"))
        {
            term["academic_term"] = request?.AcademicTerm?.Trim() ?? string.Empty;
        }

        return term;
    }

    public static int? ParseWeekNumber(string? weekLabel)
    {
        var cleaned = Regex.Replace(weekLabel ?? string.Empty, @"\s+", string.Empty);
        if (DateOnly.TryParseExact(cleaned, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _)) return null;
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

    private static string NormalizeSourceType(string sourceType, string sourceKey)
    {
        var cleaned = (sourceType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            return cleaned;
        }

        return sourceKey == "jw_schedule" ? "jw" : "mobile";
    }

    private static string ResolveSourceLabel(string sourceKey)
    {
        return sourceKey == "jw_schedule" ? "教务系统课表" : "移动教务课表";
    }

    private static string NormalizeTimestamp(object? value)
    {
        var raw = value?.ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(raw) ? DateTimeOffset.UtcNow.ToString("O") : raw.Trim();
    }

}
