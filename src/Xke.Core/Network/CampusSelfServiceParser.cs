using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Xke.Contracts.Responses;

namespace Xke.Core.Network;

public static class CampusSelfServiceParser
{
    // The observed dashboard renders each field as a .row with a text-right
    // label followed by its value column. Never inspect scripts/window.user.
    public static CampusSelfServiceSummary Parse(string html)
    {
        using var document = new HtmlParser().ParseDocument(html);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in document.QuerySelectorAll(".row"))
        {
            var label = row.FirstElementChild;
            if (label is null || !label.ClassList.Contains("text-right")) continue;
            var name = Regex.Replace(label.TextContent, @"\s+", "").TrimEnd('：', ':');
            if (name is not ("状态" or "套餐" or "计费方式" or "计费周期")) continue;
            var column = label.NextElementSibling;
            if (column is null) continue;
            var value = Regex.Replace(column.TextContent, @"\s+", " ").Trim();
            if (value.Length > 0 && value != "--") fields[name] = value;
        }

        string? start = null, end = null;
        if (fields.TryGetValue("计费周期", out var period))
        {
            var parts = period.Split('至', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                start = string.IsNullOrWhiteSpace(parts[0]) ? null : parts[0];
                end = string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1];
            }
        }

        var result = new CampusSelfServiceSummary
        {
            AccountStatus = fields.GetValueOrDefault("状态"),
            PlanName = fields.GetValueOrDefault("套餐"),
            BillingMethod = fields.GetValueOrDefault("计费方式"),
            PeriodStart = start,
            PeriodEnd = end,
        };
        if (result.AccountStatus is null && result.PlanName is null && result.BillingMethod is null && start is null && end is null)
            throw new CampusSelfServiceException("后台暂未提供套餐信息");
        return result with { Configured = true, Available = true };
    }
}
