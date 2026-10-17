using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Xke.Contracts.Models;

namespace Xke.Core.Schedule;

public sealed class JwStudentProfileParser
{
    private static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
    {
        "学号", "学生号", "姓名", "院系", "学院", "专业", "班级", "学习层次", "培养层次", "学历层次", "学生类别",
        "学制", "入学日期", "入学时间", "校区",
    };

    public StudentProfileDocument Parse(string html, string fallbackStudentId)
    {
        JwHtml.EnsureAuthenticated(html);
        using var document = JwHtml.Parse(html);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var table = document.QuerySelector("#xjkpTable");
        var personalSection = true;
        foreach (var row in (table ?? (IElement?)document.Body)?.QuerySelectorAll("tr").ToArray() ?? [])
        {
            var cells = row.Children.Where(e => e.LocalName is "td" or "th").ToArray();
            if (table is not null && cells.Length == 1 && cells[0].GetAttribute("colspan") == "8")
            {
                var heading = NormalizeLabel(JwHtml.Text(cells[0]));
                if (heading != "学籍卡片") personalSection = false;
                continue;
            }
            for (var i = 0; i < cells.Length; i++)
            {
                var text = JwHtml.Text(cells[i]);
                var colon = text.IndexOfAny(['：', ':']);
                var label = NormalizeLabel(colon >= 0 ? text[..colon] : text);
                if (!Labels.Contains(label) || (!personalSection && label is not ("入学日期" or "入学时间"))) continue;
                var value = colon >= 0 ? text[(colon + 1)..].Trim() : i + 1 < cells.Length ? ReadValue(cells[i + 1]) : string.Empty;
                fields.TryAdd(label, value);
            }
        }
        foreach (var input in document.QuerySelectorAll("input[name], select[name]"))
        {
            var name = input.GetAttribute("name") ?? string.Empty;
            if (!Labels.Contains(name) && !new[] { "xh", "xuehao", "xm", "xingming", "xymc", "academy", "zymc", "major", "bj", "class", "pycc", "level", "xq", "campus", "xz", "rxrq" }.Contains(name)) continue;
            var value = input.LocalName == "select"
                ? JwHtml.Text(input.QuerySelector("option[selected]") ?? input.QuerySelector("option"))
                : input.GetAttribute("value") ?? string.Empty;
            fields.TryAdd(name, value.Trim());
        }
        string Read(params string[] names) => names.Select(n => fields.GetValueOrDefault(n)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        var date = Read("入学日期", "入学时间", "rxrq");
        if (DateOnly.TryParseExact(date, ["yyyyMMdd", "yyyy-MM-dd"], System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var enrollment)) date = enrollment.ToString("yyyy-MM-dd");
        return new StudentProfileDocument
        {
            StudentId = Read("学号", "学生号", "xh", "xuehao") is { Length: > 0 } id ? id : fallbackStudentId,
            Name = Read("姓名", "xm", "xingming"),
            AcademyName = Read("院系", "学院", "xymc", "academy"),
            MajorName = Read("专业", "zymc", "major"),
            ClassName = Read("班级", "bj", "class"),
            TrainingLevel = Read("学习层次", "培养层次", "学历层次", "学生类别", "pycc", "level"),
            CampusName = Read("校区", "xq", "campus"),
            ProgramDurationYears = int.TryParse(Read("学制", "xz"), out var years) ? years : null,
            EnrollmentDate = date,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
    }

    private static string ReadValue(IElement cell)
    {
        if (cell.QuerySelector("select") is { } select)
            return JwHtml.Text(select.QuerySelector("option[selected]") ?? select.QuerySelector("option"));
        if (cell.QuerySelector("input[value]") is { } input) return input.GetAttribute("value")?.Trim() ?? "";
        return JwHtml.Text(cell);
    }
    private static string NormalizeLabel(string text) => Regex.Replace(text, @"\s|：|:", string.Empty);
}
