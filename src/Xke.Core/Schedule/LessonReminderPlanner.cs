using System.Globalization;
using System.Text.RegularExpressions;
using Xke.Contracts.Responses;

namespace Xke.Core.Schedule;

public sealed record LessonOccurrence(string Name, string Teacher, string Room, TimeOnly Start, TimeOnly End);

/// <summary>Uses actual cached intervals. Short breaks in one teaching block do not generate another reminder.</summary>
public static class LessonReminderPlanner
{
    private static readonly Regex TimeRange = new(@"(?<start>\d{1,2}:\d{2})\s*[-–—~～至]\s*(?<end>\d{1,2}:\d{2})", RegexOptions.Compiled);

    public static IReadOnlyList<LessonOccurrence> ForDay(IReadOnlyList<ScheduleRow> rows, DateOnly date, int week, bool weekSnapshot)
    {
        var weekday = ((int)date.DayOfWeek + 6) % 7;
        var lessons = new List<LessonOccurrence>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CourseName) || DayIndex(row.Weekday) != weekday) continue;
            if (!weekSnapshot && !(row.WeekNumbers.Count > 0 ? row.WeekNumbers : ScheduleWeekPattern.Parse(row.Weeks)).Contains(week)) continue;
            var spans = row.TimeSegments.Count > 0 ? row.TimeSegments
                : new[] { !string.IsNullOrWhiteSpace(row.TimeRange) ? row.TimeRange : row.BlockTime };
            foreach (var match in spans.SelectMany(text => TimeRange.Matches(text).Cast<Match>()))
            {
                if (!TimeOnly.TryParseExact(match.Groups["start"].Value, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                    || !TimeOnly.TryParseExact(match.Groups["end"].Value, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end <= start) continue;
                lessons.Add(new(row.CourseName, row.Teacher, row.Room, start, end));
            }
        }
        var blocks = new List<LessonOccurrence>();
        foreach (var lesson in lessons.Distinct().OrderBy(lesson => lesson.Start).ThenBy(lesson => lesson.End))
        {
            var previous = blocks.LastOrDefault();
            if (previous is not null && previous.Name == lesson.Name && previous.Teacher == lesson.Teacher
                && !string.IsNullOrWhiteSpace(previous.Room) && previous.Room == lesson.Room
                && lesson.Start >= previous.End && lesson.Start - previous.End <= TimeSpan.FromMinutes(30))
                blocks[^1] = previous with { End = lesson.End };
            else blocks.Add(lesson);
        }
        return blocks;
    }

    private static int DayIndex(string day)
    {
        var value = day.Replace("星期", "").Replace("周", "").Replace("天", "日").Trim();
        var index = Array.IndexOf(new[] { "一", "二", "三", "四", "五", "六", "日" }, value);
        return index >= 0 ? index : int.TryParse(value, out var number) && number is >= 1 and <= 7 ? number - 1 : -1;
    }
}
