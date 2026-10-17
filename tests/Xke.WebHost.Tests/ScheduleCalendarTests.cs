using Microsoft.Extensions.DependencyInjection;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Schedule;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleCalendarTests
{
    private const string Term = "2026-2027-1";

    [Fact]
    public async Task LogoutAndLogin_RestoresCalendarWithoutRestoringOldCourses()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var root = Seed(data.RootPath);
        var store = root.ForStudent("CAL001").Schedules();
        SaveWeek(store, Term, 3, "2026-09-21", "older course", "2026-10-01T00:00:00Z");
        SaveWeek(store, Term, 6, null, "latest course", "2026-10-15T00:00:00Z");
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath,
            EnableBackgroundTasks = false, ScheduleApiOptions = new ScheduleApiOptions(),
        });
        var auth = app.Services.GetRequiredService<AuthService>();
        var first = auth.DesktopBootstrap();
        auth.Logout(first.Payload.SessionId);
        var login = await auth.BeginProtocolLoginAsync(new LoginRequest { User = "synthetic-user", Password = "synthetic-password" });
        var payload = Assert.IsType<AuthPayload>(login.Payload);
        Assert.NotEqual(first.Payload.SessionId, payload.SessionId);
        var week = Assert.IsType<Dictionary<string, object?>>(payload.CachedSchedule);
        Assert.Equal("第6周", week["week_label"]);
        var dates = Assert.IsType<Dictionary<string, object?>>(week["week_dates"]);
        Assert.Equal("2026-10-12", dates["星期一"]);
        Assert.Equal("2026-10-18", dates["星期日"]);
        Assert.Equal("2026-10-15T00:00:00Z", week["updated_at"]);
        Assert.Equal("latest course", Assert.Single(Assert.IsType<List<object?>>(week["rows"]).Cast<Dictionary<string, object?>>())["course_name"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshFetch_RestoresAndPersistsOnlyMissingCalendar(bool hasSourceDate)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var root = Seed(data.RootPath);
        var store = root.ForStudent("CAL001").Schedules();
        SaveWeek(store, Term, 3, "2026-09-21", "older course", "2026-10-01T00:00:00Z");
        var id = root.Sessions().Create("synthetic-user", "CAL001", "auto").SessionId;
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath,
            EnableBackgroundTasks = false, ScheduleApiOptions = new ScheduleApiOptions(), ScheduleSnapshotSource = new UndatedSource(hasSourceDate),
        });
        var result = await app.Services.GetRequiredService<ScheduleService>().FetchPreferredAsync(id, new ScheduleRequest { Mode = "current" });
        Assert.Equal(hasSourceDate ? "2026-10-19" : "2026-10-12", Assert.IsType<Dictionary<string, object?>>(result.Payload["week_dates"])["星期一"]);
        var saved = store.LoadScheduleForWeek("第6周", ["jw_schedule"], Term)!;
        Assert.Equal(hasSourceDate ? "2026-10-19" : "2026-10-12", Assert.IsType<Dictionary<string, object?>>(saved["week_dates"])["星期一"]);
        Assert.Equal("fresh course", Assert.Single(Assert.IsType<List<object?>>(saved["rows"]).Cast<Dictionary<string, object?>>())["course_name"]);
    }

    [Fact]
    public void CalendarLookup_ExcludesOtherTermsAndAccounts_AndKeepsKnownDatesAuthoritative()
    {
        using var data = new TemporaryDirectory();
        var root = Seed(data.RootPath);
        var store = root.ForStudent("CAL001").Schedules();
        SaveWeek(store, "2025-2026-2", 3, "2026-03-02", "history", "2026-10-15T00:00:00Z");
        SaveWeek(root.ForStudent("OTHER").Schedules(), Term, 3, "2026-09-21", "other account", "2026-10-15T00:00:00Z");
        Assert.Null(store.FindSemesterStartDate(Term, ["jw_schedule"]));
        Assert.Null(store.FindSemesterStartDate("", ["jw_schedule"]));
        SaveWeek(store, Term, 3, "2026-09-21", "anchor", "2026-10-01T00:00:00Z");
        SaveWeek(store, Term, 4, null, "newer undated", "2026-10-15T00:00:00Z");
        Assert.Equal(new DateOnly(2026, 9, 7), store.FindSemesterStartDate(Term, ["jw_schedule"]));
    }

    private static XkeDataRoot Seed(string directory)
    {
        var root = new XkeDataRoot(directory);
        root.Credentials().Save(new StoredCredentialDocument { User = "synthetic-user", StudentId = "CAL001", Password = "synthetic-password" });
        var student = root.ForStudent("CAL001");
        student.SaveProfile(new StudentProfileDocument { StudentId = "CAL001", Name = "Synthetic Student" });
        student.SavePreferences(AuthSettingsCatalog.CreateDefault());
        return root;
    }

    private static void SaveWeek(XkeScheduleStore store, string term, int week, string? monday, string course, string updated)
    {
        var payload = new Dictionary<string, object?>
        {
            ["term"] = new Dictionary<string, object?> { ["academic_term"] = term, ["label"] = term },
            ["week_label"] = $"第{week}周", ["viewed_week_number"] = week, ["updated_at"] = updated,
            ["week_dates"] = monday is null ? new Dictionary<string, object?>() : new Dictionary<string, object?> { ["星期一"] = monday },
            ["available_weeks"] = new List<object?> { new Dictionary<string, object?> { ["value"] = week.ToString(), ["label"] = $"第{week}周" } },
            ["rows"] = new List<object?> { new Dictionary<string, object?> { ["course_name"] = course, ["weekday"] = "星期一", ["section"] = "第1-2节", ["section_numbers"] = new[] { 1, 2 }, ["week_numbers"] = new[] { week } } },
        };
        store.SaveSourceSchedule(payload, new(), "jw_schedule", $"第{week}周");
    }

    private sealed class UndatedSource(bool hasSourceDate) : IScheduleSnapshotSource
    {
        public Task<ScheduleSnapshotAttempt> FetchAsync(string studentId, ScheduleRequest request, CancellationToken cancellationToken)
        {
            var result = new JwScheduleFetchResult("jw_schedule", "教务系统课表", "jw", "第6周", null, 6,
                [new() { ["value"] = "6", ["label"] = "第6周" }], hasSourceDate ? new Dictionary<string, string> { ["星期一"] = "2026-10-19" } : new Dictionary<string, string>(), [], [],
                [new() { ["course_name"] = "fresh course", ["weekday"] = "星期一", ["section"] = "第1-2节", ["section_numbers"] = new[] { 1, 2 }, ["week_numbers"] = new[] { 6 } }],
                new() { ["term"] = new Dictionary<string, object?> { ["academic_term"] = Term, ["label"] = Term } }, "", "");
            return Task.FromResult(new ScheduleSnapshotAttempt(result, ""));
        }
    }
}
