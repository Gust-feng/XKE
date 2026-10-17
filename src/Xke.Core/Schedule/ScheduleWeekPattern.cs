using System.Text.RegularExpressions;

namespace Xke.Core.Schedule;

internal static class ScheduleWeekPattern
{
    public static IReadOnlyList<int> Parse(string text)
    {
        var rule = text.Split('[')[0];
        var match = Regex.Match(rule, @"(?<!\d)(?:第\s*)?(?<weeks>\d{1,2}(?:\s*[-~～至]\s*\d{1,2})?(?:\s*[,，、]\s*\d{1,2}(?:\s*[-~～至]\s*\d{1,2})?)*)\s*(?:\((?<kind>单周|双周|周)\)|(?<kind>单周|双周|周))");
        if (!match.Success)
        {
            var single = ScheduleService.ParseWeekNumber(text);
            return single is > 0 ? [single.Value] : [];
        }
        var weeks = new SortedSet<int>();
        foreach (var part in Regex.Split(match.Groups["weeks"].Value, "[,，、]"))
        {
            var bounds = Regex.Matches(part, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
            var end = bounds.Length == 2 ? bounds[1] : bounds[0];
            for (var i = bounds[0]; i <= end; i++) weeks.Add(i);
        }
        var parity = match.Groups["kind"].Value;
        return weeks.Where(w => parity != "单周" || w % 2 == 1).Where(w => parity != "双周" || w % 2 == 0).ToArray();
    }
}
