using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Schedule;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class JwDataExtractionTests
{
    private const string Profile = """
        <table id="xjkpTable">
        <tr><td colspan="8">学籍卡片</td></tr>
        <tr><td>院系：Test Academy</td><td>专业：Test Major</td><td>学制：4</td><td>班级：Test Class</td><td>学号：TEST001</td></tr>
        <tr><td>姓名</td><td>Test Student</td><td>校区</td><td></td></tr>
        <tr><td>学习层次</td><td>职业本科</td><td>学习形式</td><td></td></tr>
        <tr><td colspan="8">家庭成员情况</td></tr><tr><td>姓名</td><td>Test Relative</td></tr>
        <tr><td>入学日期</td><td>20240901</td></tr></table>
        """;

    private const string Home = """
        <li data-value="mode-test" class="layui-this" name="kbjcmsid">默认节次模式</li>
        <select id="campus"><option value="campus-test">Test Campus</option></select>
        <select><option value="">2026-2027-1</option></select>
        <select id="week"><option value="2026-09-07" selected>第一周</option><option value="2026-09-14">第二周</option></select>
        """;

    private static string Course(string name, string weeks, string sections, string room = "Test Room") =>
        $"{name}<br><font title='教师'>Test Teacher</font><br><font title='周次(节次)'>{weeks}(周)[{sections}节]</font><br><font title='教室'>{room}</font><font name='xsks'>(理论:32)</font>";

    private static string Semester(string term = "2026-2027-1", string zc = "") => $"""
        <select id="xnxq01id"><option value="2026-2027-1" {(term == "2026-2027-1" ? "selected" : "")}>2026-2027-1</option><option value="2025-2026-2" {(term == "2025-2026-2" ? "selected" : "")}>2025-2026-2</option></select>
        <select id="kbjcmsid"><option selected value="mode-test">默认节次模式</option></select>
        <select id="zc"><option value="" {(zc == "" ? "selected" : "")}>全部</option><option value="1" {(zc == "1" ? "selected" : "")}>第一周</option></select>
        <table id="timetable"><tr><td></td><td>星期一</td></tr>
        <tr><td>第三大节(05,06小节)14:00-15:40</td><td><div id="row-1-1" class="kbcontent1">short</div><div id="row-1-2" class="kbcontent">{Course("Test Course", "1-6,8-16", "05-06-07-08")}</div><div id="row-1-4" class="kbcontent">{Course("Ignore Display Copy", "1-16", "05-06")}</div></td></tr>
        <tr><td>第四大节(07,08小节)16:00-17:40</td><td><div id="row2-1-2" class="kbcontent">{Course("Test Course", "1-6,8-16", "05-06-07-08")}</div></td></tr>
        <tr><td>备注</td><td>Test note without fixed time</td></tr></table>
        """;

    [Fact]
    public void Profile_UsesCurrentHeaderAndExcludesFamilyNames()
    {
        var p = new JwStudentProfileParser().Parse(Profile, "fallback");
        Assert.Equal("TEST001", p.StudentId); Assert.Equal("Test Student", p.Name);
        Assert.Equal("Test Academy", p.AcademyName); Assert.Equal("Test Major", p.MajorName);
        Assert.Equal("Test Class", p.ClassName); Assert.Equal("职业本科", p.TrainingLevel);
        Assert.Equal(4, p.ProgramDurationYears); Assert.Equal("2024-09-01", p.EnrollmentDate); Assert.Empty(p.CampusName);
    }

    [Fact]
    public void Profile_PreservesEmptyCellsAndReadsSelectedOptions()
    {
        var p = new JwStudentProfileParser().Parse("<table><tr><th>学院</th><td></td><th>专业</th><td>Test Major</td><th>班级</th><td>Test Class</td></tr></table><select name='xq'><option>old</option><option selected>Test Campus</option></select>", "TEST001");
        Assert.Empty(p.AcademyName); Assert.Equal("Test Major", p.MajorName); Assert.Equal("Test Class", p.ClassName); Assert.Equal("Test Campus", p.CampusName);
    }

    [Fact]
    public void Semester_MergesDisplayCopiesAndRetainsServerTimeSegments()
    {
        var result = new TraditionalJwScheduleHtmlParser().ParseSemester(Semester(), "2026-2027-1");
        var row = Assert.Single(result.Rows);
        Assert.Equal("Test Course", row["course_name"]); Assert.Equal("Test Teacher", row["teacher"]);
        Assert.Equal(new[] { 5, 6, 7, 8 }, Assert.IsType<int[]>(row["section_numbers"]));
        Assert.DoesNotContain(7, Assert.IsAssignableFrom<IReadOnlyList<int>>(row["week_numbers"]));
        Assert.Equal(new[] { "14:00-15:40", "16:00-17:40" }, Assert.IsType<List<string>>(row["time_segments"]));
        Assert.Equal("", row["small_section_1"]); Assert.Equal(0, row["break_minutes"]);
        Assert.Equal(new[] { "Test note without fixed time" }, Assert.IsType<string[]>(result.RawPayload["notes"]));
    }

    [Theory]
    [InlineData("2-16(双周)[03-04节]", "2;4;6;8;10;12;14;16")]
    [InlineData("1-13,15-16(周)[03-04节]", "1;2;3;4;5;6;7;8;9;10;11;12;13;15;16")]
    [InlineData("14(周)[03-04节]", "14")]
    public void Semester_ExpandsActualWeekPatterns(string pattern, string expected)
    {
        var html = $"<table id='timetable'><tr><td>第3-4节 10:00-11:40</td><td><div class='kbcontent'>Test Course<br><font title='教师'>Test Teacher</font><font title='周次(节次)'>{pattern}</font><font title='教室'>Test Room</font></div></td></tr></table>";
        var row = Assert.Single(new TraditionalJwScheduleHtmlParser().ParseSemester(html, "2026-2027-1").Rows);
        Assert.Equal(expected, string.Join(";", Assert.IsAssignableFrom<IReadOnlyList<int>>(row["week_numbers"])));
    }

    [Fact]
    public void Semester_SplitsCoursesAndKeepsUninterpretedHours()
    {
        var html = $"<table id='timetable'><tr><td>第3-4节 10:00-11:40</td><td><div class='kbcontent'>{Course("Course A", "1-16", "03-04")}---------------------<br>{Course("Course B", "17-18", "03-04").Replace("(理论:32)", "((理论:32),实践:20)")}</div></td></tr></table>";
        var result = new TraditionalJwScheduleHtmlParser().ParseSemester(html, "2026-2027-1");
        Assert.Equal(2, result.Rows.Count); Assert.Equal("((理论:32),实践:20)", result.Rows[1]["hours_text"]); Assert.Equal("", result.Rows[1]["credit"]);
    }

    [Fact]
    public void EmptyGridIsValidButMaintenancePageIsNot()
    {
        var parser = new TraditionalJwScheduleHtmlParser();
        Assert.Empty(parser.Parse("<table id='timetable'><tr><td>第1-2节 08:00-09:40</td><td></td></tr></table>", new()).Rows);
        Assert.Throws<JwScheduleException>(() => parser.Parse("<html>Test maintenance page</html>", new()));
        Assert.Throws<TraditionalJwSessionExpiredException>(() => parser.Parse("{\"flag1\":2,\"msgContent\":\"请先登录系统\"}", new()));
    }

    [Fact]
    public void WeekdayComesFromServerHeader()
    {
        var html = $"<table id='timetable'><tr><th>节次</th><th>星期日</th></tr><tr><td>第1-2节 08:00-09:40</td><td><div class='kbcontent'>{Course("Course A", "1", "01-02")}</div></td></tr></table>";
        Assert.Equal("星期日", Assert.Single(new TraditionalJwScheduleHtmlParser().Parse(html, new()).Rows)["weekday"]);
    }

    [Fact]
    public async Task Client_QueriesHistoricalSemesterUsingOnlyGet()
    {
        var requests = new List<HttpRequestMessage>();
        using var http = new HttpClient(new Handler(r => { requests.Add(r); return Semester(r.RequestUri!.Query.Contains("2025-2026-2") ? "2025-2026-2" : "2026-2027-1"); }));
        using var client = new TraditionalJwClient("TEST001", "", "auto", httpClient: http, assumeJwSession: true);
        var result = await client.FetchSemesterScheduleAsync("2025-2026-2", new());
        Assert.Equal("2025-2026-2", result.TermLabel); Assert.Single(result.Rows);
        Assert.Equal(2, requests.Count); Assert.All(requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.Contains("xnxq01id=2025-2026-2", requests[1].RequestUri!.Query); Assert.Contains("zc=", requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task Client_UsesSelectedHomepageTermInsteadOfFirstHistoricalOption()
    {
        var home = Home.Replace("<option value=\"\">2026-2027-1</option>",
            "<option value='2025-2026-2'>2025-2026-2</option><option value='2026-2027-1' selected>2026-2027-1</option>");
        using var http = new HttpClient(new Handler(r => r.RequestUri!.AbsolutePath.EndsWith("mainV_index_loadkb.jsp")
            ? "<table id='timetable'><tr><td>第1-2节 08:00-09:40</td><td></td></tr></table>" : home));
        using var client = new TraditionalJwClient("TEST001", "", "auto", httpClient: http, assumeJwSession: true);
        var result = await client.FetchScheduleAsync(new(), new());
        var term = Assert.IsType<Dictionary<string, object?>>(result.RawPayload["term"]);
        Assert.Equal("2026-2027-1", term["academic_term"]);
    }

    [Fact]
    public async Task Client_RejectsUnavailableWeekInsteadOfFallingBack()
    {
        using var http = new HttpClient(new Handler(r => Home));
        using var client = new TraditionalJwClient("TEST001", "", "auto", httpClient: http, assumeJwSession: true);
        var ex = await Assert.ThrowsAsync<JwScheduleException>(() => client.FetchScheduleAsync(new() { Mode = "manual", ManualWeek = "99" }, new()));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Client_TreatsExpiredJsonAsSessionFailure()
    {
        using var http = new HttpClient(new Handler(r => r.RequestUri!.AbsolutePath.EndsWith("mainV_index_loadkb.jsp") ? "{\"flag1\":2}" : Home));
        using var client = new TraditionalJwClient("TEST001", "", "auto", httpClient: http, assumeJwSession: true);
        await Assert.ThrowsAsync<TraditionalJwSessionExpiredException>(() => client.FetchScheduleAsync(new(), new()));
    }

    [Fact]
    public void ProfileAndPreferences_WriteOrdinaryJson()
    {
        using var tmp = new TemporaryDirectory(); var root = new XkeDataRoot(tmp.RootPath); var store = root.ForStudent("TEST001");
        var profile = new JwStudentProfileParser().Parse(Profile, "TEST001");
        store.SaveProfile(profile); store.SavePreferences(new());
        Assert.Equal("2024-09-01", JsonDocument.Parse(File.ReadAllText(store.ProfilePath)).RootElement.GetProperty("enrollment_date").GetString());
        Assert.StartsWith("{", File.ReadAllText(store.PreferencesPath).TrimStart());
        Assert.Equal(4, store.LoadProfile()!.ProgramDurationYears);
        Assert.Equal("2024-09-01", store.LoadProfile()!.EnrollmentDate);
    }

    [Fact]
    public void SemesterSnapshot_DoesNotReplaceWeeklyCacheAndExportsNormalizedCsv()
    {
        using var tmp = new TemporaryDirectory(); var store = new XkeDataRoot(tmp.RootPath).ForStudent("TEST001").Schedules();
        var r = new TraditionalJwScheduleHtmlParser().ParseSemester(Semester(), "2026-2027-1");
        var rows = JsonSerializer.SerializeToElement(r.Rows).Deserialize<ScheduleRow[]>()!;
        var doc = new SemesterScheduleData(new("2026-2027-1", "2026-2027-1"), new("TEST001", "Test Student"), rows, [], [], [], [], DateTimeOffset.Now.ToString("O"), []);
        var saved = store.SaveSemesterSchedule(doc);
        Assert.Equal(2, saved.SavedFiles.Count);
        var csv = File.ReadAllText(Path.Combine(tmp.RootPath, saved.SavedFiles.Single(p => p.EndsWith(".csv"))));
        Assert.Contains("section_numbers,week_numbers,time_segments,weeks,credit,hours_text", csv); Assert.Contains("5;6;7;8", csv);
        Assert.Null(store.LoadScheduleForWeek("第1周", ["jw_schedule"]));
        Assert.Empty(Directory.EnumerateFiles(tmp.RootPath, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SemesterEndpoint_PersistsTypedDataAndKeepsSnapshotAfterFailedGet()
    {
        using var tmp = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(tmp.RootPath); var student = root.ForStudent("TEST001");
        root.Credentials().Save(new StoredCredentialDocument { User = "TEST001", StudentId = "TEST001", Password = "test-only" });
        student.SaveProfile(new JwStudentProfileParser().Parse(Profile, "TEST001"));
        student.SavePreferences(new AppSettingsDocument());
        var session = root.Sessions().Create("TEST001", "TEST001", "auto");
        var source = new SemesterClient();
        await using var host = await WebHostTestServer.StartAsync(web, tmp, jwScheduleClient: source);
        using var response = await host.Client.GetAsync($"/api/schedule/semester?session_id={session.SessionId}&academic_term=2026-2027-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("2026-2027-1", data.GetProperty("term").GetProperty("academic_term").GetString());
        Assert.Equal(4, data.GetProperty("rows")[0].GetProperty("section_numbers").GetArrayLength());
        Assert.Equal(2, data.GetProperty("rows")[0].GetProperty("time_segments").GetArrayLength());
        var path = Path.Combine(tmp.RootPath, data.GetProperty("saved_files")[0].GetString()!);
        var original = File.ReadAllText(path); source.Fail = true;
        using var failed = await host.Client.GetAsync($"/api/schedule/semester?session_id={session.SessionId}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode); Assert.Equal(original, File.ReadAllText(path));
        using var profileUpdate = await host.Client.PostAsJsonAsync($"/api/auth/profile?session_id={session.SessionId}", new ProfileRequest { Nickname = "Test Nickname", UseNickname = true });
        Assert.Equal(HttpStatusCode.OK, profileUpdate.StatusCode);
        var p = student.LoadProfile()!; Assert.Equal(4, p.ProgramDurationYears); Assert.Equal("2024-09-01", p.EnrollmentDate);
    }

    [Fact]
    public void CacheLookup_IsTermScopedAndIgnoresMalformedHistoricalFiles()
    {
        using var tmp = new TemporaryDirectory(); var root = new XkeDataRoot(tmp.RootPath); var store = root.ForStudent("TEST001").Schedules();
        Dictionary<string, object?> Data(string term, string title) => new()
        {
            ["term"] = new Dictionary<string, object?> { ["academic_term"] = term }, ["week_label"] = "第1周",
            ["rows"] = new List<object?> { new Dictionary<string, object?> { ["course_name"] = title, ["weekday"] = "星期一", ["section"] = "第1-2节" } },
        };
        store.SaveSourceSchedule(Data("2025-2026-2", "History"), [], "jw_schedule", "第1周");
        store.SaveSourceSchedule(Data("2026-2027-1", "Current"), [], "jw_schedule", "第1周");
        var payload = store.LoadScheduleForWeek("第1周", ["jw_schedule"], "2025-2026-2")!;
        Assert.Equal("2025-2026-2", ((Dictionary<string, object?>)payload["term"]!)["academic_term"]);
        Assert.Null(store.LoadScheduleForWeek("第1周", ["jw_schedule"], "2099-2100-1"));
        var broken = Path.Combine(store.SchedulesDirectory, "broken", "weeks", "第1周", "jw_schedule.json");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!); File.WriteAllText(broken, "{");
        Assert.NotNull(store.LoadLatestSourceSchedule(["jw_schedule"]));
    }

    [Fact]
    public void Profile_ReadsOnlySelectedValuesInPairedCells()
    {
        var html = "<table id='xjkpTable'><tr><td>专业</td><td><select name='zymc'><option>Ignore</option><option selected>Test Major</option></select></td><td>姓名</td><td><input value='Test Student'></td></tr></table>";
        var profile = new JwStudentProfileParser().Parse(html, "TEST001");
        Assert.Equal("Test Major", profile.MajorName); Assert.Equal("Test Student", profile.Name);
    }

    [Fact]
    public void NormalCourseTextContainingLoginIsNotAnExpiredSession()
    {
        var html = $"<table id='timetable'><tr><td>第1-2节 08:00-09:40</td><td><div class='kbcontent'>{Course("登录 login 原理", "1", "01-02")}</div></td></tr></table>";
        Assert.Single(new TraditionalJwScheduleHtmlParser().Parse(html, new()).Rows);
    }
    private sealed class SemesterClient : IJwScheduleClient
    {
        public bool Fail;
        private readonly DefaultFakeJwScheduleClient _weekly = new();
        public Task<JwScheduleFetchResult> FetchSemesterAsync(string user, string password, string preloginMode, string academicTerm, string sessionId, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new JwScheduleException("Test semester failure", 503);
            return Task.FromResult(new TraditionalJwScheduleHtmlParser().ParseSemester(Semester(), string.IsNullOrEmpty(academicTerm) ? "2026-2027-1" : academicTerm));
        }
        public Task<JwScheduleFetchResult> FetchPreferredAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null)
            => _weekly.FetchPreferredAsync(user, password, preloginMode, request, cancellationToken, existingToken, mobileJw);
        public Task<JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null, string sessionId = "")
            => _weekly.FetchCookieAsync(user, password, preloginMode, request, cancellationToken, existingToken, mobileJw, sessionId);
    }

    private sealed class Handler(Func<HttpRequestMessage, string> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body(request)) });
    }
}
