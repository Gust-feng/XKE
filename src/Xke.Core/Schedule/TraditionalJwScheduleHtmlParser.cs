using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

public sealed class TraditionalJwScheduleHtmlParser
{
    private static readonly string[] Weekdays = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];

    public JwScheduleFetchResult Parse(string html, ScheduleRequest request) => ParseDocument(html, request, false);
    public JwScheduleFetchResult ParseSemester(string html, string academicTerm) => ParseDocument(html,
        new ScheduleRequest { AcademicTerm = academicTerm }, true);

    private static JwScheduleFetchResult ParseDocument(string html, ScheduleRequest request, bool semester)
    {
        JwHtml.EnsureAuthenticated(html);
        using var document = JwHtml.Parse(html);
        var table = document.QuerySelector("#timetable, table.kbtable")
            ?? document.QuerySelectorAll("table").FirstOrDefault(t => t.QuerySelector(".item-box, .kbcontent") is not null
                || t.QuerySelectorAll("tr").Any(r => Cells(r).Any(c => Weekdays.Contains(JwHtml.Text(c))))
                || (t.QuerySelector("font[title]") is not null && t.QuerySelectorAll("tr").Any(r => Cells(r).FirstOrDefault() is { } c && ParseTime(c).Sections.Count > 0)));
        if (table is null) throw new JwScheduleException("教务响应不包含课表结构，未更新本地数据");
        var weekLabel = semester ? "全部周" : ResolveWeekLabel(document, request);
        var week = semester ? null : ScheduleService.ParseWeekNumber(weekLabel);
        var blocks = new List<Dictionary<string, object?>>();
        var rows = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var warnings = new List<Dictionary<string, object?>>();
        var headings = new Dictionary<int, string>();
        foreach (var tr in table.QuerySelectorAll("tr").Where(r => r.Closest("table") == table))
        {
            var cells = Cells(tr);
            for (var i = 0; i < cells.Length; i++)
                if (Weekdays.Contains(JwHtml.Text(cells[i]))) headings[i] = JwHtml.Text(cells[i]);
            if (cells.Length < 2) continue;
            var time = ParseTime(cells[0]);
            if (time.Sections.Count == 0 && time.Range.Length == 0) continue;
            blocks.Add(new Dictionary<string, object?>
            {
                ["block_label"] = time.Label, ["section"] = ScheduleSectionNormalizer.FormatSectionRanges(time.Sections),
                ["block_time"] = time.Range, ["small_section_1"] = "", ["small_section_2"] = "", ["break_minutes"] = 0,
            });
            for (var i = 1; i < Math.Min(cells.Length, 8); i++)
            {
                var weekday = headings.GetValueOrDefault(i) ?? Weekdays[i - 1];
                foreach (var course in ParseCourses(cells[i]))
                {
                    var sections = ScheduleSectionNormalizer.ExtractSectionNumbers(course.SectionText);
                    if (sections.Count == 0) sections = time.Sections.ToList();
                    var section = ScheduleSectionNormalizer.FormatSectionRanges(sections);
                    var weeksText = course.Weeks.Length > 0 ? course.Weeks : semester ? "" : weekLabel;
                    // Only identical arrangements from repeated display blocks are merged.
                    var key = string.Join('\u001f', weekday, section, course.Name, course.Teacher, course.Room, weeksText, course.Credit);
                    if (!rows.TryGetValue(key, out var row))
                    {
                        row = new Dictionary<string, object?>
                        {
                            ["source"] = "Cookie", ["week_label"] = weekLabel, ["weekday"] = weekday,
                            ["section"] = section, ["section_numbers"] = sections.ToArray(), ["course_name"] = course.Name,
                            ["teacher"] = course.Teacher, ["room"] = course.Room, ["weeks"] = weeksText,
                            ["week_numbers"] = ScheduleWeekPattern.Parse(weeksText), ["credit"] = course.Credit,
                            ["time_range"] = "", ["block_time"] = "", ["time_text"] = "",
                            ["time_segments"] = new List<string>(), ["small_section_1"] = "", ["small_section_2"] = "",
                            ["break_minutes"] = 0, ["hours_text"] = course.Hours,
                        };
                        rows.Add(key, row);
                        if (course.Teacher.Length == 0 || course.Room.Length == 0 || sections.Count == 0)
                            warnings.Add(new Dictionary<string, object?> { ["type"] = "missing_field", ["course_name"] = course.Name, ["message"] = "部分课程字段缺失，已保留可读取内容" });
                    }
                    var segments = (List<string>)row["time_segments"]!;
                    if (time.Range.Length > 0 && !segments.Contains(time.Range)) segments.Add(time.Range);
                    row["time_range"] = row["block_time"] = string.Join(" / ", segments);
                    row["time_text"] = string.Join(" ", new[] { weekday, section, row["time_range"]?.ToString() }.Where(v => !string.IsNullOrWhiteSpace(v)));
                    if (row["hours_text"] is string existing && existing != course.Hours && course.Hours.Length > 0)
                    {
                        var variants = row.GetValueOrDefault("hours_variants") as List<string> ?? [existing];
                        if (!variants.Contains(course.Hours)) variants.Add(course.Hours);
                        row["hours_variants"] = variants;
                    }
                }
            }
        }
        var notes = table.QuerySelectorAll("tr").Select(Cells).Where(c => c.Length > 1 && JwHtml.Text(c[0]).StartsWith("备注", StringComparison.Ordinal))
            .Select(c => JwHtml.Text(c[1])).Where(t => t.Length > 0).ToArray();
        var unscheduled = ParseSupplementaryCourses(document.QuerySelector("#dataTables"));
        var raw = new Dictionary<string, object?>
        {
            ["source"] = "jw", ["parser"] = "jw_dom", ["scope"] = semester ? "semester" : "week",
            ["notes"] = notes, ["supplementary_courses"] = unscheduled,
            ["term"] = new Dictionary<string, object?> { ["academic_term"] = request.AcademicTerm, ["label"] = request.TermLabel.Length > 0 ? request.TermLabel : request.AcademicTerm },
        };
        var available = semester ? [] : new List<Dictionary<string, object?>> { new() { ["value"] = week?.ToString() ?? weekLabel, ["label"] = weekLabel, ["selected"] = true } };
        return new JwScheduleFetchResult("jw_schedule", "教务系统课表", "jw", weekLabel, week, week, available,
            new Dictionary<string, string>(), blocks, warnings,
            rows.Values.OrderBy(r => Array.IndexOf(Weekdays, r["weekday"]?.ToString())).ThenBy(r => ((int[])r["section_numbers"]!).FirstOrDefault()).ToArray(),
            raw, "教务系统课表解析成功", "");
    }

    private static IElement[] Cells(IElement row) => row.Children.Where(c => c.LocalName is "td" or "th").ToArray();

    private static IEnumerable<Course> ParseCourses(IElement cell)
    {
        var items = cell.QuerySelectorAll(".item-box");
        if (items.Length > 0)
        {
            var hovers = cell.QuerySelectorAll("span.box").Where(e => e.Closest(".item-box") is null).ToArray();
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var hover = hovers.ElementAtOrDefault(i) ?? item.QuerySelector("span.box");
                var name = item.QuerySelectorAll("p").Where(p => p.Closest(".box") is null).Select(JwHtml.Text).FirstOrDefault(t => t.Length > 0) ?? "";
                if (name.Length == 0) name = hover?.QuerySelectorAll("p").Select(JwHtml.Text).FirstOrDefault(t => !t.StartsWith("教师", StringComparison.Ordinal)) ?? "";
                if (name.Length == 0) continue;
                var teacher = hover?.QuerySelectorAll("p").Select(JwHtml.Text).FirstOrDefault(t => t.StartsWith("教师", StringComparison.Ordinal)) ?? "";
                teacher = Regex.Replace(teacher, @"^教师\s*[：:]\s*", "");
                var details = item.QuerySelector(".tch-name")?.QuerySelectorAll("span").Select(JwHtml.Text).ToArray() ?? [];
                var spans = item.QuerySelectorAll("span").Where(s => s.Closest(".tch-name, .box") is null).Select(JwHtml.Text).Where(t => t.Length > 0).ToArray();
                var section = details.Skip(1).FirstOrDefault(ScheduleSectionNormalizer.LooksLikeSectionText)
                    ?? hover?.QuerySelectorAll("span.text").Select(JwHtml.Text).FirstOrDefault(ScheduleSectionNormalizer.LooksLikeSectionText) ?? "";
                var room = item.QuerySelector("img[src$='item1.png']")?.ParentElement is { } roomSpan ? JwHtml.Text(roomSpan)
                    : spans.FirstOrDefault(t => !t.Contains('周') && !t.Contains("学分", StringComparison.Ordinal) && !ScheduleSectionNormalizer.LooksLikeSectionText(t)) ?? "";
                var weeks = spans.FirstOrDefault(t => t.Contains('周')) ?? "";
                var credit = details.FirstOrDefault(t => t.Contains("学分", StringComparison.Ordinal)) ?? "";
                yield return new Course(name, teacher, room, weeks, Regex.Replace(credit, @"^学分\s*[：:]\s*", ""), section, "");
            }
            yield break;
        }
        var content = cell.QuerySelectorAll("div.kbcontent").Where(d => !Regex.IsMatch(d.Id ?? "", @"-\d+-[14]$")).ToArray();
        foreach (var container in content.Length > 0 ? content : [cell])
        {
            List<List<INode>> groups = [[]];
            foreach (var node in container.ChildNodes)
            {
                if (node is IText text && Regex.IsMatch(text.Data, @"-{5,}"))
                {
                    foreach (var part in Regex.Split(text.Data, @"(-{5,})"))
                        if (Regex.IsMatch(part, @"^-{5,}$")) groups.Add([]);
                        else if (!string.IsNullOrWhiteSpace(part)) groups[^1].Add(container.Owner!.CreateTextNode(part));
                }
                else groups[^1].Add(node);
            }
            foreach (var nodes in groups)
            {
                var fonts = nodes.OfType<IElement>().SelectMany(e => e.LocalName == "font" ? new[] { e } : e.QuerySelectorAll("font").AsEnumerable()).ToArray();
                if (fonts.Length == 0) continue;
                if (fonts.Any(f => f.GetAttribute("title") == "教师"))
                {
                    var name = nodes.OfType<IText>().Select(JwHtml.Text).FirstOrDefault(t => t.Length > 0)
                        ?? nodes.OfType<IElement>().Where(e => e.LocalName == "p").Select(JwHtml.Text).FirstOrDefault() ?? "";
                    if (name.Length == 0) continue;
                    string Field(string label) => JwHtml.Text(fonts.FirstOrDefault(f => f.GetAttribute("title") == label));
                    var rule = Field("周次(节次)");
                    var bracket = Regex.Match(rule, @"\[(?<sections>[^\]]+)\]");
                    yield return new Course(name, Field("教师"), Field("教室"), rule, "", bracket.Groups["sections"].Value,
                        JwHtml.Text(fonts.FirstOrDefault(f => f.GetAttribute("name") == "xsks")));
                }
            }
        }
    }

    private static (string Label, List<int> Sections, string Range) ParseTime(IElement cell)
    {
        var text = JwHtml.Text(cell);
        var range = Regex.Match(text, @"\d{1,2}:\d{2}\s*[-~～–—]\s*\d{1,2}:\d{2}");
        var sections = ScheduleSectionNormalizer.ExtractSectionNumbers(text);
        var label = Regex.Match(text, @"第[一二三四五六七八九十\d]+大节|上午|下午|晚上").Value;
        return (label, sections, range.Success ? Regex.Replace(range.Value, @"\s", "").Replace('～', '-').Replace('~', '-') : "");
    }

    private static string ResolveWeekLabel(IDocument document, ScheduleRequest request)
    {
        var manual = ScheduleService.ParseWeekNumber(request.ManualWeek);
        if (manual is > 0) return $"第{manual}周";
        var match = Regex.Match(JwHtml.Text(document.Body), @"第\s*(?:\d+|[一二三四五六七八九十]+)\s*周");
        return match.Success ? $"第{ScheduleService.ParseWeekNumber(match.Value)}周" : "当前周";
    }

    private static IReadOnlyList<Dictionary<string, object?>> ParseSupplementaryCourses(IElement? table)
    {
        if (table is null) return [];
        var trs = table.QuerySelectorAll("tr").ToArray();
        var headerIndex = Array.FindIndex(trs, t => Cells(t).Any(c => JwHtml.Text(c) == "课程编号"));
        if (headerIndex < 0) return [];
        var headers = Cells(trs[headerIndex]).Select(JwHtml.Text).ToArray();
        return trs.Skip(headerIndex + 1).Select(Cells).Where(c => c.Length == headers.Length)
            .Select(c => headers.Select((h, i) => (h, value: (object?)JwHtml.Text(c[i]))).ToDictionary(p => p.h, p => p.value)).ToArray();
    }

    private sealed record Course(string Name, string Teacher, string Room, string Weeks, string Credit, string SectionText, string Hours);
}
