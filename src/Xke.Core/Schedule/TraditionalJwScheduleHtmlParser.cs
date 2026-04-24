using System.Net;
using System.Text.RegularExpressions;
using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

public sealed class TraditionalJwScheduleHtmlParser
{
    private static readonly string[] Weekdays =
    [
        "星期一",
        "星期二",
        "星期三",
        "星期四",
        "星期五",
        "星期六",
        "星期日",
    ];

    public JwScheduleFetchResult Parse(string html, ScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new JwScheduleException("教务系统课表 HTML 为空");
        }

        var warnings = new List<Dictionary<string, object?>>();
        var weekLabel = ResolveWeekLabel(html, request);
        var weekNumber = ScheduleService.ParseWeekNumber(weekLabel);
        var rows = new List<Dictionary<string, object?>>();
        var blockTimes = new List<Dictionary<string, object?>>();

        foreach (Match rowMatch in Regex.Matches(html, @"<tr\b[^>]*>(?<body>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var cells = Regex.Matches(rowMatch.Groups["body"].Value, @"<td\b[^>]*>(?<body>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Cast<Match>()
                .Select(match => match.Groups["body"].Value)
                .ToArray();
            if (cells.Length < 2)
            {
                continue;
            }

            var time = ParseTimeCell(cells[0]);
            if (!string.IsNullOrWhiteSpace(time.Section) || !string.IsNullOrWhiteSpace(time.Range))
            {
                AddBlockTime(blockTimes, time);
            }

            for (var cellIndex = 1; cellIndex < cells.Length; cellIndex++)
            {
                var titles = ExtractFontTitles(cells[cellIndex]);
                if (titles.Count == 0)
                {
                    titles = SplitCellText(cells[cellIndex]);
                }

                var courseName = titles.ElementAtOrDefault(0) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(courseName))
                {
                    continue;
                }

                var teacher = titles.ElementAtOrDefault(1) ?? string.Empty;
                var room = titles.ElementAtOrDefault(2) ?? string.Empty;
                var weeks = FirstNonEmpty(titles.ElementAtOrDefault(3), weekLabel);
                var weekday = Weekdays[Math.Min(cellIndex - 1, Weekdays.Length - 1)];

                AddMissingFieldWarnings(warnings, courseName, teacher, room, time.Section);
                rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = "Cookie",
                    ["week_label"] = weekLabel,
                    ["weekday"] = weekday,
                    ["section"] = time.Section,
                    ["time_text"] = BuildTimeText(weekday, time.Section, time.Range),
                    ["time_range"] = time.Range,
                    ["block_time"] = time.Range,
                    ["small_section_1"] = string.Empty,
                    ["small_section_2"] = string.Empty,
                    ["break_minutes"] = 0,
                    ["course_name"] = courseName,
                    ["teacher"] = teacher,
                    ["room"] = room,
                    ["weeks"] = weeks,
                    ["credit"] = string.Empty,
                });
            }
        }

        if (rows.Count == 0)
        {
            AddWarning(warnings, "jw-cookie-empty", "教务系统课表 HTML 未解析出课程");
        }

        var availableWeeks = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["value"] = weekNumber?.ToString() ?? weekLabel,
                ["label"] = weekLabel,
                ["selected"] = true,
            },
        };

        var rawPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = "jw",
            ["parser"] = "traditional_jw_html",
            ["row_count"] = rows.Count,
            ["warning_count"] = warnings.Count,
        };

        return new JwScheduleFetchResult(
            SourceKey: "jw_schedule",
            SourceLabel: "教务系统课表",
            SourceType: "jw",
            WeekLabel: weekLabel,
            CurrentWeekNumber: weekNumber,
            ViewedWeekNumber: weekNumber,
            AvailableWeeks: availableWeeks,
            WeekDates: new Dictionary<string, string>(StringComparer.Ordinal),
            BlockTimes: blockTimes,
            DataWarnings: warnings,
            Rows: rows
                .OrderBy(row => WeekdaySort(row.GetValueOrDefault("weekday")?.ToString()))
                .ThenBy(row => FirstSectionNumber(row.GetValueOrDefault("section")?.ToString()) ?? 99)
                .ToArray(),
            RawPayload: rawPayload,
            Remarks: "教务系统 HTML 课表解析成功",
            FallbackReason: string.Empty);
    }

    private static string ResolveWeekLabel(string html, ScheduleRequest request)
    {
        var manual = (request.ManualWeek ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(manual))
        {
            return manual.StartsWith("第", StringComparison.Ordinal) ? manual : $"第{manual}周";
        }

        var match = Regex.Match(html, @"第\s*\d+\s*周");
        return match.Success ? Regex.Replace(match.Value, @"\s+", string.Empty) : "当前周";
    }

    private static JwTimeCell ParseTimeCell(string html)
    {
        var lines = SplitCellText(html);
        var block = lines.FirstOrDefault(line => !line.Contains('节') && !Regex.IsMatch(line, @"\d{1,2}:\d{2}")) ?? string.Empty;
        var section = lines.FirstOrDefault(line => line.Contains('节')) ?? string.Empty;
        var range = lines.FirstOrDefault(line => Regex.IsMatch(line, @"\d{1,2}:\d{2}\s*[-~]\s*\d{1,2}:\d{2}")) ?? string.Empty;
        range = Regex.Replace(range, @"\s+", string.Empty).Replace('~', '-');
        return new JwTimeCell(block, section, range);
    }

    private static List<string> ExtractFontTitles(string html)
    {
        return Regex.Matches(html, @"<font\b[^>]*\btitle\s*=\s*[""'](?<title>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Select(match => Decode(match.Groups["title"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static List<string> SplitCellText(string html)
    {
        var withBreaks = Regex.Replace(html ?? string.Empty, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        var text = StripHtml(withBreaks);
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Decode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static void AddBlockTime(List<Dictionary<string, object?>> blockTimes, JwTimeCell time)
    {
        if (blockTimes.Any(item =>
                string.Equals(item.GetValueOrDefault("section")?.ToString(), time.Section, StringComparison.Ordinal)
                && string.Equals(item.GetValueOrDefault("block_time")?.ToString(), time.Range, StringComparison.Ordinal)))
        {
            return;
        }

        blockTimes.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["block_label"] = time.Block,
            ["section"] = time.Section,
            ["block_time"] = time.Range,
            ["small_section_1"] = string.Empty,
            ["small_section_2"] = string.Empty,
            ["break_minutes"] = 0,
        });
    }

    private static void AddMissingFieldWarnings(
        List<Dictionary<string, object?>> warnings,
        string courseName,
        string teacher,
        string room,
        string section)
    {
        if (string.IsNullOrWhiteSpace(teacher))
        {
            AddWarning(warnings, "jw-cookie-teacher-missing", $"{courseName} 缺少教师字段");
        }

        if (string.IsNullOrWhiteSpace(room))
        {
            AddWarning(warnings, "jw-cookie-room-missing", $"{courseName} 缺少教室字段");
        }

        if (string.IsNullOrWhiteSpace(section))
        {
            AddWarning(warnings, "jw-cookie-section-missing", $"{courseName} 缺少节次字段");
        }
    }

    private static string BuildTimeText(string weekday, string section, string range)
    {
        return string.Join(" / ", new[] { weekday, section, range }
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int? FirstSectionNumber(string? value)
    {
        var match = Regex.Match(value ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, out var parsed) ? parsed : null;
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

    private static void AddWarning(List<Dictionary<string, object?>> warnings, string type, string message)
    {
        warnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["message"] = message,
        });
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string StripHtml(string value)
    {
        return Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty).Trim();
    }

    private static string Decode(string value)
    {
        return WebUtility.HtmlDecode(value ?? string.Empty).Trim();
    }

    private sealed record JwTimeCell(string Block, string Section, string Range);
}
