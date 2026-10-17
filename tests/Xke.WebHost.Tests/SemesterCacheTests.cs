using System.Net;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Schedule;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class SemesterCacheTests
{
    [Fact]
    public async Task LocalSemester_IsAvailableOfflineAfterRestart_AndNeverQueriesSchool()
    {
        using var temp = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(temp.RootPath);
        var student = root.ForStudent("CACHE001");
        root.Credentials().Save(new StoredCredentialDocument { User = "test-account", StudentId = "CACHE001", Password = "test-only" });
        student.SaveProfile(new StudentProfileDocument { StudentId = "CACHE001", Name = "Test Student" });
        var schedule = Snapshot("2026-2027-1", "2026-10-10T01:00:00Z", "Room A");
        student.Schedules().SaveSemesterSchedule(schedule);
        var sessionId = root.Sessions().Create("test-account", "CACHE001", "auto").SessionId;
        var school = new DefaultFakeJwScheduleClient { CookieException = new IOException("No school requests permitted") };
        await using var host = await WebHostTestServer.StartAsync(web, temp, jwScheduleClient: school);
        host.Client.DefaultRequestHeaders.Add(SessionConstants.SessionHeaderName, sessionId);

        using var response = await host.Client.GetAsync("/api/schedule/semester/local?academic_term=2026-2027-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("cached").GetBoolean());
        Assert.Equal(schedule.UpdatedAt, data.GetProperty("updated_at").GetString());
        Assert.Equal("Room A", data.GetProperty("rows")[0].GetProperty("room").GetString());
        Assert.Equal(0, school.CookieCalls);
        Assert.Equal(0, school.CookieCalls);
        using var missing = await host.Client.GetAsync("/api/schedule/semester/local?academic_term=2025-2026-2");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public void SemesterStore_KeepsTermsSeparate_PreservesNewerData_AndAcceptsNewEmptyResult()
    {
        using var temp = new TemporaryDirectory();
        var store = new XkeDataRoot(temp.RootPath).ForStudent("CACHE001").Schedules();
        store.SaveSemesterSchedule(Snapshot("2025-2026-2", "2026-10-10T00:00:00Z", "History"));
        store.SaveSemesterSchedule(Snapshot("2026-2027-1", "2026-10-10T02:00:00Z", "Latest"));
        var older = store.SaveSemesterSchedule(Snapshot("2026-2027-1", "2026-10-10T01:00:00Z", "Stale"));
        Assert.True(older.Cached);
        Assert.Equal("Latest", Assert.Single(older.Rows).Room);
        Assert.Equal("History", Assert.Single(store.LoadSemesterSchedule("2025-2026-2")!.Rows).Room);
        Assert.Equal("2026-2027-1", store.LoadSemesterSchedule()!.Term.AcademicTerm);
        Assert.Null(store.LoadSemesterSchedule("2024-2025-1"));

        var empty = Snapshot("2026-2027-1", "2026-10-10T03:00:00Z", "") with { Rows = [] };
        store.SaveSemesterSchedule(empty);
        var reloaded = new XkeDataRoot(temp.RootPath).ForStudent("CACHE001").Schedules().LoadSemesterSchedule("2026-2027-1")!;
        Assert.Empty(reloaded.Rows);
        Assert.Equal(empty.UpdatedAt, reloaded.UpdatedAt);
        Assert.Single(File.ReadAllLines(Path.Combine(temp.RootPath, reloaded.SavedFiles.Single(f => f.EndsWith(".csv")))));
    }

    [Fact]
    public void DefaultSemester_DoesNotFollowTheMostRecentlyReadArchive()
    {
        using var temp = new TemporaryDirectory();
        var store = new XkeDataRoot(temp.RootPath).ForStudent("CACHE001").Schedules();
        store.SaveSemesterSchedule(Snapshot("2026-2027-1", "2026-10-10T00:00:00Z", "Current"));
        store.SaveSemesterSchedule(Snapshot("2025-2026-2", "2026-10-13T00:00:00Z", "Archive"));
        Assert.Equal("2026-2027-1", store.LoadSemesterSchedule()!.Term.AcademicTerm);
        Assert.Equal("Archive", Assert.Single(store.LoadSemesterSchedule("2025-2026-2")!.Rows).Room);
    }

    [Fact]
    public void SharedProjection_KeepsArrangementIdentity_UnknownFields_AndServerDates()
    {
        var parser = new TraditionalJwScheduleHtmlParser();
        var html = """
            <table id="timetable"><tr><td></td><td>星期一</td></tr>
            <tr><td>第1-2节 08:00-09:40</td><td><div class="kbcontent">Test Course
            <font title="教师">Test Teacher</font><font title="周次(节次)">1-16(周)[01-02节]</font><font title="教室">Room A</font></div></td></tr></table>
            """;
        var result = parser.Parse(html, new ScheduleRequest { Mode = "manual", ManualWeek = "一", AcademicTerm = "2026-2027-1" });
        var row = new Dictionary<string, object?>(Assert.Single(result.Rows)) { ["unknown_field"] = "server detail" };
        var different = new Dictionary<string, object?>(row) { ["room"] = "Room B", ["weeks"] = "2-16(双周)", ["week_numbers"] = new[] { 2, 4, 6 } };
        result.RawPayload["updated_at"] = "2026-10-10T01:00:00Z";
        result = result with { Rows = [row, new Dictionary<string, object?>(row), different], AvailableWeeks =
            [new Dictionary<string, object?> { ["label"] = "第一周", ["value"] = "2026-09-07" }] };
        var profile = new StudentProfileDocument { StudentId = "CACHE001", Name = "Test Student" };
        var week = ScheduleDataMapper.FromWeek(result, profile);
        var rows = Assert.IsType<List<object?>>(week["rows"]).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal("server detail", row["unknown_field"]));
        Assert.Equal("第1周", week["week_label"]);
        Assert.Equal("2026-09-13", Assert.IsType<Dictionary<string, object?>>(week["week_dates"])["星期日"]);
        Assert.Equal("2026-10-10T01:00:00Z", week["updated_at"]);
        var semester = ScheduleDataMapper.FromSemester(result, profile);
        Assert.Equal(2, semester.Rows.Count);
        Assert.Equal(new[] { 2, 4, 6 }, semester.Rows.Single(r => r.Room == "Room B").WeekNumbers);
        Assert.Equal(week["updated_at"], semester.UpdatedAt);
    }

    private static SemesterScheduleData Snapshot(string term, string updatedAt, string room)
        => new(new ScheduleTermSummary(term, term), new ScheduleStudentSummary("CACHE001", "Test Student"),
            [new ScheduleRow { CourseName = "Test Course", Weekday = "星期一", Section = "第1-2节", SectionNumbers = [1, 2],
                WeekNumbers = [1, 2], Room = room }], [], [new ScheduleTermOption(term, term, true)], [], [], updatedAt, []);
}
