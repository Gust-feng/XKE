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
        var blockTimes = new List<Dictionary<string, object?>>();
        var parsedRows = new List<ParsedScheduleRow>();

        foreach (var tableRow in ExtractTableRows(html))
        {
            if (tableRow.Cells.Count < 2)
            {
                continue;
            }

            var time = ParseTimeCell(tableRow.Cells[0]);
            if (string.IsNullOrWhiteSpace(time.Section) && string.IsNullOrWhiteSpace(time.Range))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(time.Section) || !string.IsNullOrWhiteSpace(time.Range))
            {
                AddBlockTime(blockTimes, time);
            }

            for (var cellIndex = 1; cellIndex < Math.Min(tableRow.Cells.Count, 8); cellIndex++)
            {
                var weekday = Weekdays[Math.Min(cellIndex - 1, Weekdays.Length - 1)];
                foreach (var course in ParseCourses(tableRow.Cells[cellIndex]))
                {
                    if (string.IsNullOrWhiteSpace(course.CourseName))
                    {
                        continue;
                    }

                    var courseSections = ExtractSectionNumbers(course.SectionText);
                    if (courseSections.Count == 0)
                    {
                        courseSections = time.SectionNumbers.ToList();
                    }

                    var smallSections = ResolveSmallSections(courseSections, time.SectionTimeMap);
                    var row = BuildScheduleRow(
                        source: "Cookie",
                        weekLabel: weekLabel,
                        weekday: weekday,
                        section: FormatSectionRanges(courseSections),
                        courseName: course.CourseName,
                        teacher: course.Teacher,
                        room: course.Room,
                        weeks: FirstNonEmpty(course.WeekInfo, weekLabel),
                        timeRange: time.Range,
                        blockTime: time.Range,
                        smallSection1: smallSections.FirstSmallSection,
                        smallSection2: smallSections.SecondSmallSection,
                        breakMinutes: !string.IsNullOrWhiteSpace(smallSections.FirstSmallSection)
                            && !string.IsNullOrWhiteSpace(smallSections.SecondSmallSection)
                            ? 10
                            : 0,
                        credit: course.Credit);

                    AddMissingFieldWarnings(
                        warnings,
                        row.GetValueOrDefault("course_name")?.ToString() ?? course.CourseName,
                        row.GetValueOrDefault("teacher")?.ToString() ?? string.Empty,
                        row.GetValueOrDefault("room")?.ToString() ?? string.Empty,
                        row.GetValueOrDefault("section")?.ToString() ?? string.Empty);

                    parsedRows.Add(new ParsedScheduleRow(row, courseSections));
                }
            }
        }

        var rows = AggregateRows(parsedRows, warnings);
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

    private static List<ParsedTableRow> ExtractTableRows(string html)
    {
        var rows = new List<ParsedTableRow>();
        foreach (Match rowMatch in Regex.Matches(html, @"<tr\b[^>]*>(?<body>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var cells = Regex.Matches(rowMatch.Groups["body"].Value, @"<td\b[^>]*>(?<body>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Cast<Match>()
                .Select(match => match.Groups["body"].Value)
                .ToList();
            if (cells.Count > 0)
            {
                rows.Add(new ParsedTableRow(cells));
            }
        }

        return rows;
    }

    private static List<ParsedCourse> ParseCourses(string cellHtml)
    {
        var itemBoxes = ExtractElementsByClass(cellHtml, "div", "item-box");
        if (itemBoxes.Count > 0)
        {
            var hoverBoxes = ExtractElementsByClass(cellHtml, "span", "box");
            return itemBoxes
                .Select((itemBox, index) => ParseItemBoxCourse(itemBox, hoverBoxes.ElementAtOrDefault(index) ?? string.Empty))
                .Where(course => !string.IsNullOrWhiteSpace(course.CourseName))
                .ToList();
        }

        var titles = ExtractFontTitles(cellHtml);
        if (titles.Count == 0)
        {
            titles = SplitCellText(cellHtml);
        }

        if (titles.Count == 0 || string.IsNullOrWhiteSpace(titles[0]))
        {
            return [];
        }

        return
        [
            new ParsedCourse(
                CourseName: titles.ElementAtOrDefault(0) ?? string.Empty,
                Teacher: NormalizeTeacherText(titles.ElementAtOrDefault(1) ?? string.Empty),
                Room: StripHtml(titles.ElementAtOrDefault(2) ?? string.Empty),
                WeekInfo: StripHtml(titles.ElementAtOrDefault(3) ?? string.Empty),
                Credit: NormalizeCreditText(titles.ElementAtOrDefault(4) ?? string.Empty),
                SectionText: FirstNonEmpty(titles.FirstOrDefault(LooksLikeSectionText), string.Empty)),
        ];
    }

    private static ParsedCourse ParseItemBoxCourse(string itemHtml, string pairedBoxHtml)
    {
        var boxHtml = FirstNonEmpty(pairedBoxHtml, ExtractElementsByClass(itemHtml, "span", "box").FirstOrDefault());
        var itemWithoutBox = RemoveElementsByClass(itemHtml, "span", "box");
        var tchHtml = ExtractElementsByClass(itemHtml, "div", "tch-name").FirstOrDefault() ?? string.Empty;
        var itemWithoutBoxAndTch = RemoveElementsByClass(itemWithoutBox, "div", "tch-name");

        var boxParagraphs = ExtractElementTexts(boxHtml, "p");
        var shortTitle = boxParagraphs.FirstOrDefault(value => !value.Contains("教师", StringComparison.Ordinal)) ?? string.Empty;
        var boxTeacher = boxParagraphs.FirstOrDefault(value => value.Contains("教师", StringComparison.Ordinal)) ?? string.Empty;
        var boxSpans = ExtractElementTexts(boxHtml, "span");
        var boxSection = boxSpans.FirstOrDefault(LooksLikeSectionText) ?? string.Empty;

        var title = ExtractElementTexts(RemoveElementsByClass(itemWithoutBoxAndTch, "span", "text"), "p").FirstOrDefault() ?? string.Empty;
        var tchSpans = ExtractElementTexts(tchHtml, "span");
        var credit = tchSpans.FirstOrDefault() ?? string.Empty;
        var section = tchSpans.Skip(1).FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(section))
        {
            section = boxSection;
        }

        var infoSpans = ExtractElementTexts(itemWithoutBoxAndTch, "span")
            .Where(value => !string.Equals(value, shortTitle, StringComparison.Ordinal))
            .Where(value => !value.Contains("教师", StringComparison.Ordinal))
            .Where(value => !value.Contains("学分", StringComparison.Ordinal))
            .Where(value => !LooksLikeSectionText(value))
            .ToList();
        var room = infoSpans.FirstOrDefault(value => !value.Contains("周", StringComparison.Ordinal)) ?? string.Empty;
        var weekInfo = infoSpans.FirstOrDefault(value => value.Contains("周", StringComparison.Ordinal)) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(room) && infoSpans.Count > 0)
        {
            room = infoSpans[0];
        }

        if (string.IsNullOrWhiteSpace(weekInfo) && infoSpans.Count > 1)
        {
            weekInfo = infoSpans[1];
        }

        return new ParsedCourse(
            CourseName: FirstNonEmpty(title, shortTitle),
            Teacher: NormalizeTeacherText(boxTeacher),
            Room: StripHtml(room),
            WeekInfo: NormalizeWeekText(weekInfo),
            Credit: NormalizeCreditText(credit),
            SectionText: section);
    }

    private static JwTimeCell ParseTimeCell(string html)
    {
        var lines = SplitCellText(html);
        var header = string.Join(" ", lines);
        var sectionNumbers = ExtractSectionNumbers(header);
        var section = FormatSectionRanges(sectionNumbers);
        var rangeMatch = Regex.Match(header, @"(\d{1,2}:\d{2})\s*[-~]\s*(\d{1,2}:\d{2})");
        var range = rangeMatch.Success ? $"{rangeMatch.Groups[1].Value}-{rangeMatch.Groups[2].Value}" : string.Empty;
        var block = lines.FirstOrDefault(line => !line.Contains('节') && !Regex.IsMatch(line, @"\d{1,2}:\d{2}")) ?? string.Empty;
        var (firstSmall, secondSmall) = SplitTwoSections(rangeMatch.Groups[1].Value, rangeMatch.Groups[2].Value);
        var sectionTimeMap = new Dictionary<int, string>();
        if (sectionNumbers.Count >= 2 && !string.IsNullOrWhiteSpace(firstSmall) && !string.IsNullOrWhiteSpace(secondSmall))
        {
            sectionTimeMap[sectionNumbers[0]] = firstSmall;
            sectionTimeMap[sectionNumbers[1]] = secondSmall;
        }

        return new JwTimeCell(block, section, range, sectionNumbers, sectionTimeMap);
    }

    private static (string FirstSmallSection, string SecondSmallSection) ResolveSmallSections(
        IReadOnlyList<int> sectionNumbers,
        IReadOnlyDictionary<int, string> sectionTimeMap)
    {
        var slots = sectionNumbers
            .Select(number => sectionTimeMap.GetValueOrDefault(number) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return (
            slots.ElementAtOrDefault(0) ?? string.Empty,
            slots.ElementAtOrDefault(1) ?? string.Empty);
    }

    private static Dictionary<string, object?> BuildScheduleRow(
        string source,
        string weekLabel,
        string weekday,
        string section,
        string courseName,
        string teacher,
        string room,
        string weeks,
        string timeRange,
        string blockTime,
        string smallSection1,
        string smallSection2,
        int breakMinutes,
        string credit)
    {
        var normalizedWeekday = NormalizeWeekdayText(weekday);
        var normalizedSection = NormalizeSectionText(section);
        var normalizedWeeks = NormalizeWeekText(weeks);
        var normalizedTimeRange = StripHtml(timeRange);
        var timeText = BuildTimeText(normalizedWeekday, normalizedSection, normalizedTimeRange);

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = source,
            ["week_label"] = weekLabel,
            ["weekday"] = normalizedWeekday,
            ["section"] = normalizedSection,
            ["time_text"] = timeText,
            ["time_range"] = normalizedTimeRange,
            ["block_time"] = StripHtml(blockTime),
            ["small_section_1"] = StripHtml(smallSection1),
            ["small_section_2"] = StripHtml(smallSection2),
            ["break_minutes"] = breakMinutes,
            ["course_name"] = StripHtml(courseName),
            ["teacher"] = NormalizeTeacherText(teacher),
            ["room"] = StripHtml(room),
            ["weeks"] = normalizedWeeks,
            ["credit"] = NormalizeCreditText(credit),
        };
    }

    private static List<Dictionary<string, object?>> AggregateRows(
        IReadOnlyList<ParsedScheduleRow> parsedRows,
        List<Dictionary<string, object?>> warnings)
    {
        var aggregated = new Dictionary<string, AggregatedScheduleRow>(StringComparer.Ordinal);
        foreach (var parsed in parsedRows)
        {
            var row = parsed.Row;
            var key = string.Join(
                "\u001f",
                row.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("section")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("teacher")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("weeks")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("credit")?.ToString() ?? string.Empty);
            if (!aggregated.TryGetValue(key, out var existing))
            {
                var rooms = SplitRoomCandidates(row.GetValueOrDefault("room")?.ToString());
                aggregated[key] = new AggregatedScheduleRow(row, parsed.SectionNumbers.ToList(), rooms);
                continue;
            }

            MergeSections(existing, parsed);
            MergeRooms(existing, parsed, warnings);
        }

        return aggregated.Values.Select(item => item.Row).ToList();
    }

    private static void MergeSections(AggregatedScheduleRow existing, ParsedScheduleRow parsed)
    {
        var merged = existing.SectionNumbers.Concat(parsed.SectionNumbers)
            .Where(number => number is >= 1 and <= 20)
            .Distinct()
            .Order()
            .ToList();
        if (merged.Count > 0)
        {
            existing.SectionNumbers.Clear();
            existing.SectionNumbers.AddRange(merged);
            existing.Row["section"] = FormatSectionRanges(merged);
            existing.Row["time_text"] = BuildTimeText(
                existing.Row.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
                existing.Row.GetValueOrDefault("section")?.ToString() ?? string.Empty,
                existing.Row.GetValueOrDefault("time_range")?.ToString() ?? string.Empty);
        }

        MergeTextField(existing.Row, parsed.Row, "time_range", " / ");
        MergeTextField(existing.Row, parsed.Row, "block_time", " / ");
        FillTextField(existing.Row, parsed.Row, "small_section_1");
        FillTextField(existing.Row, parsed.Row, "small_section_2");
        if (!string.IsNullOrWhiteSpace(existing.Row.GetValueOrDefault("small_section_1")?.ToString())
            && !string.IsNullOrWhiteSpace(existing.Row.GetValueOrDefault("small_section_2")?.ToString()))
        {
            existing.Row["break_minutes"] = 10;
        }
    }

    private static void MergeRooms(
        AggregatedScheduleRow existing,
        ParsedScheduleRow parsed,
        List<Dictionary<string, object?>> warnings)
    {
        var incomingRooms = SplitRoomCandidates(parsed.Row.GetValueOrDefault("room")?.ToString());
        var addedRooms = new List<string>();
        foreach (var room in incomingRooms)
        {
            if (existing.Rooms.Contains(room, StringComparer.Ordinal))
            {
                continue;
            }

            existing.Rooms.Add(room);
            addedRooms.Add(room);
        }

        if (addedRooms.Count == 0 || existing.Rooms.Count <= 1)
        {
            return;
        }

        existing.Row["room"] = string.Join(" / ", existing.Rooms);
        existing.Row["conflict"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = "room_conflict",
            ["kept_room"] = existing.Rooms[0],
            ["discarded_rooms"] = existing.Rooms.Skip(1).Cast<object?>().ToList(),
        };

        warnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "duplicate_slot",
            ["course_name"] = existing.Row.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
            ["weekday"] = existing.Row.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
            ["section"] = existing.Row.GetValueOrDefault("section")?.ToString() ?? string.Empty,
            ["kept_room"] = existing.Rooms[0],
            ["kept_weeks"] = existing.Row.GetValueOrDefault("weeks")?.ToString() ?? string.Empty,
            ["discarded"] = addedRooms
                .Select(room => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["room"] = room,
                })
                .ToList(),
            ["conflict"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "room_conflict",
            },
            ["message"] = "检测到同课程同节次存在多个教室，已保留全部教室",
        });
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
            ["small_section_1"] = time.SectionTimeMap.Count > 0 ? time.SectionTimeMap.Values.ElementAtOrDefault(0) ?? string.Empty : string.Empty,
            ["small_section_2"] = time.SectionTimeMap.Count > 1 ? time.SectionTimeMap.Values.ElementAtOrDefault(1) ?? string.Empty : string.Empty,
            ["break_minutes"] = time.SectionTimeMap.Count > 1 ? 10 : 0,
        });
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
        withBreaks = Regex.Replace(withBreaks, @"</?(?:p|div|span|li)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        var text = StripHtml(withBreaks);
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Decode)
            .Select(NormalizeSpaces)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static List<string> ExtractElementTexts(string html, string tagName)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        return Regex.Matches(html, $@"<{Regex.Escape(tagName)}\b[^>]*>(?<body>.*?)</{Regex.Escape(tagName)}>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Select(match => NormalizeSpaces(StripHtml(match.Groups["body"].Value)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static List<string> ExtractElementsByClass(string html, string tagName, string className)
    {
        var elements = new List<string>();
        if (string.IsNullOrWhiteSpace(html))
        {
            return elements;
        }

        foreach (Match match in Regex.Matches(html, $@"<{Regex.Escape(tagName)}\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            if (!HasClass(match.Groups["attrs"].Value, className))
            {
                continue;
            }

            var element = ExtractBalancedElement(html, tagName, match.Index);
            if (!string.IsNullOrWhiteSpace(element))
            {
                elements.Add(element);
            }
        }

        return elements;
    }

    private static string RemoveElementsByClass(string html, string tagName, string className)
    {
        var result = html ?? string.Empty;
        foreach (var element in ExtractElementsByClass(result, tagName, className))
        {
            result = result.Replace(element, string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    private static string ExtractBalancedElement(string html, string tagName, int startIndex)
    {
        var tagPattern = $@"</?{Regex.Escape(tagName)}\b[^>]*>";
        var depth = 0;
        foreach (Match match in Regex.Matches(html[startIndex..], tagPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var token = match.Value;
            var absoluteIndex = startIndex + match.Index;
            var isClosing = token.StartsWith("</", StringComparison.Ordinal);
            var isSelfClosing = token.EndsWith("/>", StringComparison.Ordinal);
            if (!isClosing && !isSelfClosing)
            {
                depth++;
            }
            else if (isClosing)
            {
                depth--;
            }

            if (depth == 0)
            {
                return html[startIndex..(absoluteIndex + token.Length)];
            }
        }

        return string.Empty;
    }

    private static bool HasClass(string attrs, string className)
    {
        var match = Regex.Match(attrs, @"\bclass\s*=\s*[""'](?<class>[^""']*)[""']", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        return match.Groups["class"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, className, StringComparison.Ordinal));
    }

    private static (string FirstSmallSection, string SecondSmallSection) SplitTwoSections(string startText, string endText, int breakMinutes = 10)
    {
        var start = ParseClock(startText);
        var end = ParseClock(endText);
        if (start is null || end is null || end <= start)
        {
            return (string.Empty, string.Empty);
        }

        var total = end.Value - start.Value;
        int firstEnd;
        int secondStart;
        if (total > breakMinutes + 1)
        {
            var oneSlot = (total - breakMinutes) / 2;
            firstEnd = start.Value + oneSlot;
            secondStart = firstEnd + breakMinutes;
        }
        else
        {
            var oneSlot = total / 2;
            firstEnd = start.Value + oneSlot;
            secondStart = firstEnd;
        }

        return ($"{FormatClock(start.Value)}-{FormatClock(firstEnd)}", $"{FormatClock(secondStart)}-{FormatClock(end.Value)}");
    }

    private static int? ParseClock(string value)
    {
        var match = Regex.Match(value ?? string.Empty, @"^(\d{1,2}):(\d{2})$");
        if (!match.Success)
        {
            return null;
        }

        return int.Parse(match.Groups[1].Value) * 60 + int.Parse(match.Groups[2].Value);
    }

    private static string FormatClock(int totalMinutes)
    {
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }

    private static List<int> ExtractSectionNumbers(string? value)
    {
        return ScheduleSectionNormalizer.ExtractSectionNumbers(value);
    }

    private static string FormatSectionRanges(IReadOnlyList<int> numbers)
    {
        return ScheduleSectionNormalizer.FormatSectionRanges(numbers);
    }

    private static string NormalizeSectionText(string value)
    {
        return ScheduleSectionNormalizer.NormalizeSectionText(value);
    }

    private static string NormalizeWeekdayText(string value)
    {
        var cleaned = StripHtml(value);
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

    private static string NormalizeTeacherText(string value)
    {
        return StripHtml(value).Replace("教师：", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string NormalizeCreditText(string value)
    {
        var cleaned = StripHtml(value).Replace("学分：", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        cleaned = Regex.Replace(cleaned, @"第?\d+(?:-\d+)?节$", string.Empty).Trim();
        var match = Regex.Match(cleaned, @"\d+(?:\.\d+)?");
        return match.Success ? match.Value : cleaned;
    }

    private static string NormalizeWeekText(string value)
    {
        return NormalizeSpaces(StripHtml(value));
    }

    private static bool LooksLikeSectionText(string value)
    {
        return ScheduleSectionNormalizer.LooksLikeSectionText(value);
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

    private static void AddWarning(List<Dictionary<string, object?>> warnings, string type, string message)
    {
        warnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["message"] = message,
        });
    }

    private static string BuildTimeText(string weekday, string section, string range)
    {
        return string.Join(" / ", new[] { weekday, section, range }
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int? FirstSectionNumber(string? value)
    {
        var numbers = ExtractSectionNumbers(value);
        return numbers.Count > 0 ? numbers.Min() : null;
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

    private static List<string> SplitRoomCandidates(string? value)
    {
        return (value ?? string.Empty)
            .Split(["/", "／", "|", "、"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSpaces)
            .Where(room => !string.IsNullOrWhiteSpace(room))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void MergeTextField(Dictionary<string, object?> target, Dictionary<string, object?> source, string key, string separator)
    {
        var current = target.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        var incoming = source.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(incoming) || current.Contains(incoming, StringComparison.Ordinal))
        {
            return;
        }

        target[key] = string.IsNullOrWhiteSpace(current) ? incoming : $"{current}{separator}{incoming}";
    }

    private static void FillTextField(Dictionary<string, object?> target, Dictionary<string, object?> source, string key)
    {
        if (!string.IsNullOrWhiteSpace(target.GetValueOrDefault(key)?.ToString()))
        {
            return;
        }

        var incoming = source.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            target[key] = incoming;
        }
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
        var withoutTags = Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty);
        return Decode(withoutTags);
    }

    private static string Decode(string value)
    {
        return WebUtility.HtmlDecode(value ?? string.Empty).Trim();
    }

    private static string NormalizeSpaces(string value)
    {
        return Regex.Replace(Decode(value), @"\s+", " ").Trim();
    }

    private sealed record ParsedTableRow(IReadOnlyList<string> Cells);

    private sealed record ParsedCourse(
        string CourseName,
        string Teacher,
        string Room,
        string WeekInfo,
        string Credit,
        string SectionText);

    private sealed record ParsedScheduleRow(Dictionary<string, object?> Row, IReadOnlyList<int> SectionNumbers);

    private sealed record JwTimeCell(
        string Block,
        string Section,
        string Range,
        IReadOnlyList<int> SectionNumbers,
        IReadOnlyDictionary<int, string> SectionTimeMap);

    private sealed record AggregatedScheduleRow(
        Dictionary<string, object?> Row,
        List<int> SectionNumbers,
        List<string> Rooms);
}
