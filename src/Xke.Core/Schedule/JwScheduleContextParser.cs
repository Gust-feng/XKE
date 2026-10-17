using AngleSharp.Dom;
using System.Text.RegularExpressions;
using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

internal static class JwScheduleContextParser
{
    public static TraditionalJwScheduleParams ParseHome(string html, ScheduleRequest request)
    {
        JwHtml.EnsureAuthenticated(html);
        using var document = JwHtml.Parse(html);
        var modes = document.QuerySelectorAll("li[name='kbjcmsid'][data-value]")
            .Select(e => new Dictionary<string, object?> { ["value"] = e.GetAttribute("data-value"), ["label"] = JwHtml.Text(e), ["selected"] = e.ClassList.Contains("layui-this") }).ToList();
        var weeks = Options(document.QuerySelector("select#week"));
        if (modes.Count == 0 || weeks.Count == 0) throw new JwScheduleException("教务首页缺少课表周次或节次模式");
        var current = weeks.FirstOrDefault(w => w["selected"] is true) ?? weeks[0];
        var selected = current;
        if (!string.IsNullOrWhiteSpace(request.CookieRq))
            selected = weeks.FirstOrDefault(w => w["value"]?.ToString() == request.CookieRq)
                ?? throw new JwScheduleException("指定的课表日期不在可查询周目录中", 404);
        else if (request.Mode.Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            var number = ScheduleService.ParseWeekNumber(request.ManualWeek);
            selected = weeks.FirstOrDefault(w => ScheduleService.ParseWeekNumber(w["label"]?.ToString() ?? "") == number)
                ?? throw new JwScheduleException("指定周不在可查询周目录中", 404);
        }
        var terms = document.QuerySelectorAll("select:not(#week)").SelectMany(s => Options(s))
            .Where(o => Regex.IsMatch(o["label"]?.ToString() ?? "", @"^\d{4}-\d{4}-[12]$")).ToList();
        var term = (terms.FirstOrDefault(o => o["selected"] is true) ?? terms.FirstOrDefault())?["label"]?.ToString() ?? "";
        var mode = modes.FirstOrDefault(m => m["selected"] is true) ?? modes[0];
        return new TraditionalJwScheduleParams(selected["value"]?.ToString() ?? "", mode["value"]?.ToString() ?? "", term,
            selected["label"]?.ToString() ?? "", current["label"]?.ToString() ?? "", weeks, modes);
    }

    public static List<Dictionary<string, object?>> Options(IElement? select) => select?.QuerySelectorAll("option")
        .Where(o => JwHtml.Text(o).Length > 0 && (o.GetAttribute("value")?.Length > 0 || Regex.IsMatch(JwHtml.Text(o), @"^\d{4}-\d{4}-[12]$")))
        .Select(o => new Dictionary<string, object?> { ["value"] = o.GetAttribute("value") ?? "", ["label"] = JwHtml.Text(o), ["selected"] = o.HasAttribute("selected") }).ToList() ?? [];
}
