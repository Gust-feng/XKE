using System.Text.Json;
using System.Text.RegularExpressions;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Storage;

namespace Xke.Core.Schedule;

public sealed class NativeJwScheduleClient : IJwScheduleClient
{
    private static readonly Uri MobileBaseUri = new("http://222.243.161.213:81/hnrjzyxyhd/");
    private readonly HttpClient _client;
    private readonly MobileJwLoginClient _loginClient;

    public NativeJwScheduleClient(HttpClient client)
        : this(client, new MobileJwLoginClient(client))
    {
    }

    public NativeJwScheduleClient(HttpClient client, MobileJwLoginClient loginClient)
    {
        _client = client;
        _loginClient = loginClient;
        _client.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<JwScheduleFetchResult> FetchPreferredAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string existingToken = "",
        MobileJwEncryptedCredentialDocument? mobileJw = null)
    {
        MobileJwEncryptedCredentialDocument? refreshedMobileJw = null;
        var token = (existingToken ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            var login = await LoginAsync(user, password, mobileJw, cancellationToken).ConfigureAwait(false);
            token = login.Token;
            refreshedMobileJw = login.Credential;
        }

        var resolution = await ResolveWeekNumberAsync(
            user,
            password,
            token,
            request,
            allowTokenRefresh: true,
            mobileJw,
            cancellationToken).ConfigureAwait(false);
        token = resolution.Token;
        refreshedMobileJw ??= resolution.MobileJw;

        Dictionary<string, object?> rawPayload;
        try
        {
            rawPayload = await FetchMobileScheduleRawAsync(token, resolution.WeekNumber, cancellationToken).ConfigureAwait(false);
        }
        catch (JwScheduleException) when (!string.IsNullOrWhiteSpace(existingToken))
        {
            var login = await LoginAsync(user, password, mobileJw, cancellationToken).ConfigureAwait(false);
            token = login.Token;
            refreshedMobileJw = login.Credential;
            rawPayload = await FetchMobileScheduleRawAsync(token, resolution.WeekNumber, cancellationToken).ConfigureAwait(false);
        }

        return BuildPreferredResult(rawPayload, request) with { MobileJw = refreshedMobileJw };
    }

    public Task<JwScheduleFetchResult> FetchCookieAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string existingToken = "",
        MobileJwEncryptedCredentialDocument? mobileJw = null)
    {
        throw new JwScheduleException("Cookie 课表原生刷新暂不可用，请使用移动教务来源刷新课表");
    }

    private async Task<MobileWeekResolution> ResolveWeekNumberAsync(
        string user,
        string password,
        string token,
        ScheduleRequest request,
        bool allowTokenRefresh,
        MobileJwEncryptedCredentialDocument? mobileJw,
        CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode == "manual")
        {
            var manualWeek = (request.ManualWeek ?? string.Empty).Trim();
            if (!Regex.IsMatch(manualWeek, @"^\d+$"))
            {
                throw new JwScheduleException("手动周次必须是数字。");
            }

            return new MobileWeekResolution(manualWeek, token, null);
        }

        if (mode != "next")
        {
            return new MobileWeekResolution(string.Empty, token, null);
        }

        MobileJwEncryptedCredentialDocument? refreshedMobileJw = null;
        Dictionary<string, object?> current;
        try
        {
            current = await FetchMobileScheduleRawAsync(token, string.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (JwScheduleException) when (allowTokenRefresh)
        {
            var login = await LoginAsync(user, password, mobileJw, cancellationToken).ConfigureAwait(false);
            token = login.Token;
            refreshedMobileJw = login.Credential;
            current = await FetchMobileScheduleRawAsync(token, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        var currentWeek = ResolveWeekValue(current);
        if (!int.TryParse(currentWeek, out var currentWeekNumber))
        {
            if (!allowTokenRefresh)
            {
                throw new JwScheduleException("无法从移动教务课表接口识别当前周次。");
            }

            if (refreshedMobileJw is null)
            {
                var login = await LoginAsync(user, password, mobileJw, cancellationToken).ConfigureAwait(false);
                token = login.Token;
                refreshedMobileJw = login.Credential;
                current = await FetchMobileScheduleRawAsync(token, string.Empty, cancellationToken).ConfigureAwait(false);
                currentWeek = ResolveWeekValue(current);
            }
        }

        if (!int.TryParse(currentWeek, out currentWeekNumber))
        {
            throw new JwScheduleException("无法从移动教务课表接口识别当前周次。");
        }

        return new MobileWeekResolution((currentWeekNumber + 1).ToString(), token, refreshedMobileJw);
    }

    private async Task<Dictionary<string, object?>> FetchMobileScheduleRawAsync(
        string token,
        string weekNumber,
        CancellationToken cancellationToken)
    {
        var uri = MobileJwLoginClient.AddQuery(new Uri(MobileBaseUri, "student/curriculum"), new Dictionary<string, string>
        {
            ["week"] = weekNumber,
            ["kbjcmsid"] = string.Empty,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        MobileJwLoginClient.ApplyMobileHeaders(request, token);
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var rawPayload = await ReadJsonDictionaryAsync(response, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessCode(rawPayload))
        {
            throw new JwScheduleException(ReadMessage(rawPayload, "课表接口返回异常"));
        }

        return rawPayload;
    }

    private JwScheduleFetchResult BuildPreferredResult(Dictionary<string, object?> rawPayload, ScheduleRequest request)
    {
        var block = ResolveScheduleBlock(rawPayload);
        var weekValue = ResolveWeekValue(rawPayload);
        var weekLabel = string.IsNullOrWhiteSpace(weekValue) ? "当前周" : $"第{weekValue}周";
        var warnings = new List<Dictionary<string, object?>>();
        var rows = BuildRows(rawPayload, block, weekValue, warnings);
        if (rows.Count == 0)
        {
            throw new JwScheduleException("移动教务课表返回中未找到可归一化的课程行");
        }

        var weekDates = ExtractWeekDates(rawPayload);
        var currentWeekNumber = int.TryParse(weekValue, out var parsedWeek) ? parsedWeek : (int?)null;
        var availableWeeks = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["value"] = string.IsNullOrWhiteSpace(weekValue) ? string.Empty : weekValue,
                ["label"] = weekLabel,
                ["selected"] = true,
            },
        };
        MobileJwDiagnosticsRecorder.RecordNote("schedule.curriculum.result", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["week_label"] = weekLabel,
            ["rows_count"] = rows.Count,
            ["warnings_count"] = warnings.Count,
        });

        return new JwScheduleFetchResult(
            SourceKey: "mobile_schedule",
            SourceLabel: "移动教务课表",
            SourceType: "mobile",
            WeekLabel: weekLabel,
            CurrentWeekNumber: currentWeekNumber,
            ViewedWeekNumber: currentWeekNumber,
            AvailableWeeks: availableWeeks,
            WeekDates: weekDates,
            BlockTimes: [],
            DataWarnings: warnings,
            Rows: rows,
            RawPayload: rawPayload,
            Remarks: "移动教务课表刷新成功",
            FallbackReason: string.Empty);
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildRows(
        Dictionary<string, object?> rawPayload,
        Dictionary<string, object?> block,
        string weekValue,
        List<Dictionary<string, object?>> warnings)
    {
        var courses = ExtractCourseItems(rawPayload, block)
            .OfType<Dictionary<string, object?>>()
            .ToArray();
        if (courses.Length == 0)
        {
            AddWarning(warnings, "mobile-course-list-missing", "移动教务返回中未识别到课程数组");
            return [];
        }

        var currentWeek = int.TryParse(weekValue, out var parsedWeek) ? parsedWeek : (int?)null;
        var rows = new List<Dictionary<string, object?>>();
        for (var index = 0; index < courses.Length; index++)
        {
            var course = courses[index];
            if (currentWeek is not null && !CourseAppliesToWeek(course, currentWeek.Value))
            {
                continue;
            }

            var (weekday, section) = DecodeCompactClassTime(GetString(course, "classTime", "class_time", "kcsj", "kssj"));
            if (string.IsNullOrWhiteSpace(weekday))
            {
                weekday = WeekdayFromDigit(GetString(course, "weekDay", "weekday", "week_day", "xqid", "xqj", "xq", "dayOfWeek"))
                    ?? NormalizeWeekdayText(GetString(course, "weekdayName", "xqjmc", "weekDayName", "dayName"));
            }

            if (string.IsNullOrWhiteSpace(section))
            {
                section = NormalizeSectionText(GetString(course, "timeStr", "time_text", "jc", "jcs", "section", "sections", "lesson", "lessonNo", "lesson_no", "node"));
            }

            var courseName = GetString(course, "courseName", "course_name", "kcmc", "kcm", "name", "curriculumName", "kkkcmc");
            var teacher = NormalizeTeacherText(GetString(course, "teaName", "teacherName", "teacher_name", "skls", "teachers", "teacher", "jsxm"));
            var room = GetString(course, "classroomName", "classroom_name", "jsmc", "roomName", "room_name", "skdd", "location", "classroom", "address");
            if (string.IsNullOrWhiteSpace(courseName))
            {
                AddWarning(warnings, "mobile-course-name-missing", $"第 {index + 1} 条课程缺少课程名，已跳过");
                continue;
            }

            if (string.IsNullOrWhiteSpace(weekday))
            {
                AddWarning(warnings, "mobile-course-weekday-missing", $"{courseName} 缺少星期字段");
            }

            if (string.IsNullOrWhiteSpace(section))
            {
                AddWarning(warnings, "mobile-course-section-missing", $"{courseName} 缺少节次字段");
            }

            var weekLabel = string.IsNullOrWhiteSpace(weekValue) ? string.Empty : $"第{weekValue}周";
            var timeRange = GetString(course, "sksj", "courseTime", "course_time", "timeRange", "time_range", "time", "skTime");
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "加密",
                ["week_label"] = string.IsNullOrWhiteSpace(weekLabel) ? "当前周" : weekLabel,
                ["weekday"] = weekday,
                ["section"] = section,
                ["time_text"] = BuildTimeText(weekday, section, timeRange),
                ["time_range"] = timeRange,
                ["block_time"] = string.Empty,
                ["small_section_1"] = string.Empty,
                ["small_section_2"] = string.Empty,
                ["break_minutes"] = 0,
                ["course_name"] = courseName,
                ["teacher"] = teacher,
                ["room"] = room,
                ["weeks"] = !string.IsNullOrWhiteSpace(weekLabel)
                    ? weekLabel
                    : GetString(course, "weekText", "week_text", "zc", "weeks", "week", "skzc", "zcd", "classWeekDetails", "sksj"),
                ["credit"] = NormalizeCreditText(GetString(course, "credit", "xf", "xfxx", "score")),
            });
        }

        return rows
            .OrderBy(row => WeekdaySort(row.GetValueOrDefault("weekday")?.ToString()))
            .ThenBy(row => FirstSectionNumber(row.GetValueOrDefault("section")?.ToString()) ?? 99)
            .ToArray();
    }

    private static bool CourseAppliesToWeek(Dictionary<string, object?> course, int week)
    {
        var details = GetString(course, "classWeekDetails", "class_week_details", "weekText", "week_text", "weeks", "skzc", "zcd", "zc");
        if (string.IsNullOrWhiteSpace(details))
        {
            return true;
        }

        return Regex.Matches(details, @"\d+")
            .Select(match => int.TryParse(match.Value, out var value) ? value : (int?)null)
            .Any(value => value == week);
    }

    private static Dictionary<string, string> ExtractWeekDates(Dictionary<string, object?> rawPayload)
    {
        var weekDates = new Dictionary<string, string>(StringComparer.Ordinal);
        var block = ResolveScheduleBlock(rawPayload);
        var dateItems = ExtractDateItems(rawPayload, block);
        var weekdays = new[] { "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" };
        for (var index = 0; index < dateItems.Count; index++)
        {
            if (dateItems[index] is not Dictionary<string, object?> item)
            {
                continue;
            }

            var rawDate = GetString(item, "mxrq", "date", "day", "rq", "calendarDate", "calendar_date");
            var match = Regex.Match(rawDate, @"\d{4}[-/]\d{1,2}[-/]\d{1,2}");
            if (!match.Success)
            {
                continue;
            }

            var weekday = WeekdayFromDigit(GetString(item, "xqid", "weekDay", "weekday", "week_day", "xqj")) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weekday) && index < weekdays.Length)
            {
                weekday = weekdays[index];
            }

            if (!string.IsNullOrWhiteSpace(weekday))
            {
                weekDates[weekday] = NormalizeDateText(match.Value);
            }
        }

        return weekDates;
    }

    private async Task<MobileJwLoginResult> LoginAsync(
        string user,
        string password,
        MobileJwEncryptedCredentialDocument? mobileJw,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _loginClient.LoginAsync(user, password, mobileJw, cancellationToken).ConfigureAwait(false);
        }
        catch (MobileJwLoginException ex)
        {
            throw new JwScheduleException(ex.Message);
        }
    }

    private static async Task<Dictionary<string, object?>> ReadJsonDictionaryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        MobileJwDiagnosticsRecorder.RecordResponse(
            response.RequestMessage?.RequestUri?.AbsolutePath.Trim('/').Replace('/', '-') ?? "mobile-response",
            response,
            body);
        if (!response.IsSuccessStatusCode)
        {
            throw new JwScheduleException($"教务接口返回 HTTP {(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return LegacyNoticeJson.ToDictionary(document.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            throw new JwScheduleException($"教务接口返回了非 JSON 响应: {ex.Message}");
        }
    }

    private static bool IsSuccessCode(Dictionary<string, object?> payload)
    {
        var rawCode = GetValue(payload, "code");
        var codeSuccess = rawCode switch
        {
            string text => IsSuccessCodeText(text),
            long number => number is 1 or 200,
            int number => number is 1 or 200,
            double number => Math.Abs(number - 1d) < 0.0001d || Math.Abs(number - 200d) < 0.0001d,
            _ => false,
        };
        if (codeSuccess)
        {
            return true;
        }

        var success = GetValue(payload, "success") ?? GetValue(payload, "ok");
        return success switch
        {
            bool value => value,
            string text => text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1",
            _ => false,
        };
    }

    private static bool IsSuccessCodeText(string text)
    {
        var cleaned = (text ?? string.Empty).Trim();
        return cleaned is "1" or "200" || cleaned.Equals("success", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMessage(Dictionary<string, object?> payload, string fallback)
    {
        foreach (var source in EnumerateDictionaries(payload))
        {
            foreach (var key in new[] { "Msg", "msg", "message", "error", "errMsg", "errmsg" })
            {
                var message = GetValue(source, key)?.ToString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }
            }
        }

        return fallback;
    }

    private static string ReadToken(Dictionary<string, object?> payload)
    {
        return ReadValue(payload, "token", "accessToken", "access_token", "jwt", "sid");
    }

    private static string ReadValue(Dictionary<string, object?> payload, params string[] keys)
    {
        foreach (var source in EnumerateDictionaries(payload))
        {
            foreach (var key in keys)
            {
                var value = GetValue(source, key)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<Dictionary<string, object?>> EnumerateDictionaries(Dictionary<string, object?> payload)
    {
        yield return payload;
        foreach (var key in new[] { "data", "result", "payload", "body" })
        {
            var nested = FirstDictionary(GetValue(payload, key));
            if (nested.Count > 0)
            {
                yield return nested;
            }
        }
    }

    private static string ResolveWeekValue(Dictionary<string, object?> rawPayload)
    {
        var block = ResolveScheduleBlock(rawPayload);
        var week = GetString(block, "week", "currentWeek", "current_week", "zc", "weekNo", "week_no");
        if (!string.IsNullOrWhiteSpace(week))
        {
            return FirstNumberText(week);
        }

        foreach (var date in ExtractDateItems(rawPayload, block).OfType<Dictionary<string, object?>>())
        {
            week = GetString(date, "zc", "week", "weekNo", "week_no");
            if (!string.IsNullOrWhiteSpace(week))
            {
                return FirstNumberText(week);
            }
        }

        var firstCourse = ExtractCourseItems(rawPayload, block).OfType<Dictionary<string, object?>>().FirstOrDefault();
        if (firstCourse is not null)
        {
            week = GetString(firstCourse, "zc", "week", "weekText", "week_text", "skzc", "classWeekDetails");
            if (!string.IsNullOrWhiteSpace(week))
            {
                return FirstNumberText(week);
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, object?> ResolveScheduleBlock(Dictionary<string, object?> rawPayload)
    {
        var data = GetValue(rawPayload, "data");
        if (data is not null)
        {
            var block = FirstDictionary(data);
            if (block.Count > 0)
            {
                return block;
            }
        }

        return FirstDictionary(rawPayload);
    }

    private static IReadOnlyList<object?> ExtractCourseItems(Dictionary<string, object?> rawPayload, Dictionary<string, object?> block)
    {
        foreach (var source in new[] { block, FirstDictionary(rawPayload) })
        {
            foreach (var key in new[]
            {
                "courses", "courseList", "course_list", "kbList", "kb_list",
                "curriculumList", "curriculum_list", "list", "rows"
            })
            {
                var items = NormalizeList(GetValue(source, key));
                if (items.Count > 0)
                {
                    return items;
                }
            }
        }

        return [];
    }

    private static IReadOnlyList<object?> ExtractDateItems(Dictionary<string, object?> rawPayload, Dictionary<string, object?> block)
    {
        foreach (var source in new[] { block, FirstDictionary(rawPayload) })
        {
            foreach (var key in new[] { "date", "dates", "weekDates", "week_dates", "days" })
            {
                var items = NormalizeList(GetValue(source, key));
                if (items.Count > 0)
                {
                    return items;
                }
            }
        }

        return [];
    }

    private static Dictionary<string, object?> FirstDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        return NormalizeList(value).OfType<Dictionary<string, object?>>().FirstOrDefault()
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static List<object?> NormalizeList(object? value)
    {
        return value switch
        {
            null => [],
            Dictionary<string, object?> dictionary => [dictionary],
            List<object?> list => list,
            IEnumerable<object?> values => values.ToList(),
            System.Collections.IEnumerable values => values.Cast<object?>().ToList(),
            _ => [],
        };
    }

    private static string GetString(Dictionary<string, object?> source, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = ToScalarString(GetValue(source, key));
            if (!string.IsNullOrWhiteSpace(value))
            {
                return StripHtml(value);
            }
        }

        return string.Empty;
    }

    private static object? GetValue(Dictionary<string, object?> source, string key)
    {
        if (source.TryGetValue(key, out var value))
        {
            return value;
        }

        foreach (var pair in source)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string ToScalarString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            },
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string BuildTimeText(string weekday, string section, string range)
    {
        var parts = new[] { weekday, section, range }
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return string.Join(" / ", parts);
    }

    private static (string Weekday, string Section) DecodeCompactClassTime(string value)
    {
        var compact = Regex.Replace(value ?? string.Empty, @"\D", string.Empty);
        if (compact.Length < 3)
        {
            return (string.Empty, NormalizeSectionText(value));
        }

        var weekday = WeekdayFromDigit(compact[0].ToString()) ?? string.Empty;
        var tail = compact[1..];
        if (tail.Length % 2 != 0)
        {
            return (weekday, NormalizeSectionText(value));
        }

        var sections = new List<int>();
        for (var index = 0; index < tail.Length; index += 2)
        {
            if (int.TryParse(tail.Substring(index, 2), out var section))
            {
                sections.Add(section);
            }
        }

        return (weekday, FormatSectionRanges(sections));
    }

    private static string NormalizeSectionText(string? value)
    {
        return FormatSectionRanges(ExtractSectionNumbers(value ?? string.Empty));
    }

    private static int? FirstSectionNumber(string? value)
    {
        return ExtractSectionNumbers(value ?? string.Empty).FirstOrDefault();
    }

    private static List<int> ExtractSectionNumbers(string value)
    {
        var numbers = new List<int>();
        foreach (Match explicitMatch in Regex.Matches(value ?? string.Empty, @"(\d{1,2})\s*[-~]\s*(\d{1,2})"))
        {
            var start = int.Parse(explicitMatch.Groups[1].Value);
            var end = int.Parse(explicitMatch.Groups[2].Value);
            if (start <= end)
            {
                numbers.AddRange(Enumerable.Range(start, end - start + 1));
            }
            else
            {
                numbers.Add(start);
                numbers.Add(end);
            }
        }

        if (numbers.Count == 0)
        {
            foreach (Match match in Regex.Matches(value ?? string.Empty, @"\d+"))
            {
                if (int.TryParse(match.Value, out var parsed) && parsed is >= 1 and <= 20)
                {
                    numbers.Add(parsed);
                }
            }
        }

        return numbers.Distinct().OrderBy(value => value).ToList();
    }

    private static string FormatSectionRanges(IReadOnlyList<int> numbers)
    {
        if (numbers.Count == 0)
        {
            return string.Empty;
        }

        var ranges = new List<string>();
        var start = numbers[0];
        var previous = numbers[0];
        foreach (var number in numbers.Skip(1))
        {
            if (number == previous + 1)
            {
                previous = number;
                continue;
            }

            ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
            start = previous = number;
        }

        ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
        return $"第{string.Join(",", ranges)}节";
    }

    private static string? WeekdayFromDigit(string value)
    {
        return (value ?? string.Empty).Trim() switch
        {
            "1" => "星期一",
            "2" => "星期二",
            "3" => "星期三",
            "4" => "星期四",
            "5" => "星期五",
            "6" => "星期六",
            "7" or "0" => "星期日",
            _ => null,
        };
    }

    private static string NormalizeWeekdayText(string value)
    {
        var match = Regex.Match(value ?? string.Empty, @"星期[一二三四五六日天]");
        return match.Success ? match.Value.Replace("星期天", "星期日") : string.Empty;
    }

    private static int WeekdaySort(string? value)
    {
        return value switch
        {
            "星期一" => 1,
            "星期二" => 2,
            "星期三" => 3,
            "星期四" => 4,
            "星期五" => 5,
            "星期六" => 6,
            "星期日" => 7,
            _ => 99,
        };
    }

    private static string NormalizeTeacherText(string value)
    {
        return StripHtml(value).Replace("教师：", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string NormalizeCreditText(string value)
    {
        var cleaned = StripHtml(value);
        var match = Regex.Match(cleaned, @"\d+(?:\.\d+)?");
        return match.Success ? match.Value : cleaned;
    }

    private sealed record MobileWeekResolution(
        string WeekNumber,
        string Token,
        MobileJwEncryptedCredentialDocument? MobileJw);

    private static string NormalizeDateText(string value)
    {
        return (value ?? string.Empty).Replace('/', '-').Trim();
    }

    private static string FirstNumberText(string value)
    {
        var match = Regex.Match(value ?? string.Empty, @"\d+");
        return match.Success ? match.Value : string.Empty;
    }

    private static void AddWarning(List<Dictionary<string, object?>> warnings, string type, string message)
    {
        warnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["message"] = message,
        });
    }

    private static string StripHtml(string value)
    {
        return Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty).Trim();
    }
}
