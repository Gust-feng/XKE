using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Schedule;

namespace Xke.WebHost.Tests;

internal sealed class DefaultFakeJwScheduleClient : IJwScheduleClient
{
    public bool FailPreferred { get; set; }
    public Exception? CookieException { get; set; }
    public Exception? PreferredException { get; set; }
    public int PreferredCalls { get; private set; }
    public int CookieCalls { get; private set; }

    public Task<JwScheduleFetchResult> FetchPreferredAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string existingToken = "",
        MobileJwEncryptedCredentialDocument? mobileJw = null)
    {
        PreferredCalls++;
        if (FailPreferred)
        {
            throw new JwScheduleException("移动教务课表初始化失败");
        }

        if (PreferredException is not null)
        {
            throw PreferredException;
        }

        return Task.FromResult(BuildResult(user));
    }

    public Task<JwScheduleFetchResult> FetchCookieAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string existingToken = "",
        MobileJwEncryptedCredentialDocument? mobileJw = null,
        string sessionId = "")
    {
        CookieCalls++;
        if (CookieException is not null) throw CookieException;
        return Task.FromResult(BuildResult(user, sourceKey: "jw_schedule", sourceLabel: "教务系统课表", sourceType: "jw", weekNumber: 8));
    }

    private static JwScheduleFetchResult BuildResult(
        string user,
        string sourceKey = "mobile_schedule",
        string sourceLabel = "移动教务课表",
        string sourceType = "mobile",
        int weekNumber = 7)
    {
        var weekLabel = $"第{weekNumber}周";
        return new JwScheduleFetchResult(
            SourceKey: sourceKey,
            SourceLabel: sourceLabel,
            SourceType: sourceType,
            WeekLabel: weekLabel,
            CurrentWeekNumber: weekNumber,
            ViewedWeekNumber: weekNumber,
            AvailableWeeks:
            [
                new Dictionary<string, object?>
                {
                    ["value"] = weekNumber.ToString(),
                    ["label"] = weekLabel,
                    ["selected"] = true,
                },
            ],
            WeekDates: new Dictionary<string, string>
            {
                ["星期一"] = "2026-04-20",
                ["星期日"] = "2026-04-26",
            },
            BlockTimes:
            [
                new Dictionary<string, object?>
                {
                    ["block_label"] = "上午",
                    ["section"] = "第1-2节",
                    ["block_time"] = "08:00-09:35",
                    ["small_section_1"] = "08:00-08:45",
                    ["small_section_2"] = "08:50-09:35",
                    ["break_minutes"] = 5,
                },
            ],
            DataWarnings: [],
            Rows:
            [
                new Dictionary<string, object?>
                {
                    ["source"] = sourceType == "jw" ? "Cookie" : "加密",
                    ["week_label"] = weekLabel,
                    ["weekday"] = "星期一",
                    ["section"] = "第1-2节",
                    ["time_text"] = "星期一 / 第1-2节 / 08:00-09:35",
                    ["time_range"] = "08:00-09:35",
                    ["course_name"] = "高等数学",
                    ["teacher"] = "张老师",
                    ["room"] = "1-101",
                    ["weeks"] = weekLabel,
                    ["credit"] = "4",
                },
            ],
            RawPayload: new Dictionary<string, object?>
            {
                ["student_id"] = user,
                ["source"] = sourceType,
            },
            Remarks: sourceType == "jw" ? "Cookie 课表刷新成功" : "移动教务课表初始化成功",
            FallbackReason: string.Empty);
    }
}
