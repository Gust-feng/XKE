using System.Net;
using System.Net.Http.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Notice;
using Xke.Core.Schedule;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleWebHostTests
{
    [Fact]
    public async Task ScheduleEndpoints_RefreshPreferredCacheAndLoadLocal()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-data");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: fakeAuth,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginData = GetData(await ReadJsonAsync(login));
        var sessionId = loginData.GetProperty("session_id").GetString()!;
        Assert.Equal("live_fetch", loginData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());
        Assert.Equal("第7周", loginData.GetProperty("cached_schedule").GetProperty("week_label").GetString());
        Assert.Equal(1, fakeSchedule.PreferredCalls);

        var refresh = await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var refreshDoc = await ReadJsonAsync(refresh);
        var refreshData = GetData(refreshDoc);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal("live_fetch", refreshData.GetProperty("data_origin").GetString());
        Assert.Equal("mobile", refreshData.GetProperty("source_type").GetString());
        Assert.Equal("第7周", refreshData.GetProperty("week_label").GetString());
        Assert.Equal("信息工程学院", refreshData.GetProperty("student").GetProperty("academy_name").GetString());
        Assert.Equal("软件技术", refreshData.GetProperty("student").GetProperty("major_name").GetString());
        Assert.Equal("专科", refreshData.GetProperty("student").GetProperty("training_level").GetString());
        var savedFiles = refreshData.GetProperty("saved_files").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(savedFiles, path => path.EndsWith("mobile_schedule.csv", StringComparison.Ordinal));
        Assert.True(savedFiles.Length >= 3);
        Assert.Equal(2, fakeSchedule.PreferredCalls);
        Assert.Equal("mobile-token-initial", fakeSchedule.LastExistingToken);

        using var localRequest = new HttpRequestMessage(HttpMethod.Get, "/api/schedule/local?week_label=第7周&source=preferred");
        localRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var local = await client.SendAsync(localRequest);
        var localDoc = await ReadJsonAsync(local);
        var localData = GetData(localDoc);

        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.True(localData.GetProperty("cached").GetBoolean());
        Assert.Equal("local_cache", localData.GetProperty("data_origin").GetString());
        Assert.Equal("高等数学", localData.GetProperty("rows")[0].GetProperty("course_name").GetString());
        Assert.Equal("信息工程学院", localData.GetProperty("student").GetProperty("academy_name").GetString());
        Assert.Equal("软件技术", localData.GetProperty("student").GetProperty("major_name").GetString());
        Assert.Equal("专科", localData.GetProperty("student").GetProperty("training_level").GetString());
    }

    [Fact]
    public async Task ScheduleEndpoints_ManualPreferredKeepsCurrentWeekDistinctFromViewedWeek()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-manual-week");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient
        {
            PreferredCurrentWeekNumber = 8,
            PreferredViewedWeekNumber = 8,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: fakeAuth,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginData = GetData(await ReadJsonAsync(login));
        var sessionId = loginData.GetProperty("session_id").GetString()!;
        fakeSchedule.PreferredViewedWeekNumber = 9;

        var refresh = await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "manual",
            ManualWeek = "9",
        });
        var refreshData = GetData(await ReadJsonAsync(refresh));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal("第9周", refreshData.GetProperty("week_label").GetString());
        Assert.Equal("第8周", refreshData.GetProperty("current_week_label").GetString());
        Assert.Equal(8, refreshData.GetProperty("current_week_number").GetInt32());
        Assert.Equal(9, refreshData.GetProperty("viewed_week_number").GetInt32());

        var local = await client.GetAsync($"/api/schedule/local?session_id={sessionId}&week_label=第9周&source=preferred");
        var localData = GetData(await ReadJsonAsync(local));

        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.Equal("第9周", localData.GetProperty("week_label").GetString());
        Assert.Equal("第8周", localData.GetProperty("current_week_label").GetString());
        Assert.Equal(8, localData.GetProperty("current_week_number").GetInt32());
        Assert.Equal(9, localData.GetProperty("viewed_week_number").GetInt32());
    }

    [Fact]
    public async Task ScheduleEndpoints_PreservesExplicitWeekNumbersWhenWeekLabelIsNotParseable()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-week-number-preserve");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient
        {
            PreferredCurrentWeekNumber = 8,
            PreferredViewedWeekNumber = 8,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: fakeAuth,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginData = GetData(await ReadJsonAsync(login));
        var sessionId = loginData.GetProperty("session_id").GetString()!;
        fakeSchedule.PreferredViewedWeekNumber = 9;
        fakeSchedule.PreferredWeekLabelOverride = "下一教学周";

        var refresh = await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "manual",
            ManualWeek = "9",
        });
        var refreshData = GetData(await ReadJsonAsync(refresh));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal("下一教学周", refreshData.GetProperty("week_label").GetString());
        Assert.Equal("第8周", refreshData.GetProperty("current_week_label").GetString());
        Assert.Equal(8, refreshData.GetProperty("current_week_number").GetInt32());
        Assert.Equal(9, refreshData.GetProperty("viewed_week_number").GetInt32());

        var localWeekLabel = Uri.EscapeDataString("下一教学周");
        var local = await client.GetAsync($"/api/schedule/local?session_id={sessionId}&week_label={localWeekLabel}&source=preferred");
        var localData = GetData(await ReadJsonAsync(local));

        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.Equal("下一教学周", localData.GetProperty("week_label").GetString());
        Assert.Equal("第8周", localData.GetProperty("current_week_label").GetString());
        Assert.Equal(8, localData.GetProperty("current_week_number").GetInt32());
        Assert.Equal(9, localData.GetProperty("viewed_week_number").GetInt32());
    }

    [Fact]
    public async Task ScheduleEndpoints_CookieRouteRefreshesJwCacheAndPreferredStaysMobileOnly()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-cookie");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient();

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: fakeAuth,
            jwScheduleClient: fakeSchedule);
        var client = host.Client;

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        });
        var loginData = GetData(await ReadJsonAsync(login));
        var sessionId = loginData.GetProperty("session_id").GetString()!;
        Assert.True(loginData.GetProperty("connected").GetBoolean());
        Assert.Equal("live_fetch", loginData.GetProperty("cached_schedule").GetProperty("data_origin").GetString());

        var cookieRefresh = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var cookieData = GetData(await ReadJsonAsync(cookieRefresh));

        Assert.Equal(HttpStatusCode.OK, cookieRefresh.StatusCode);
        Assert.Equal("jw_schedule", cookieData.GetProperty("source_key").GetString());
        Assert.Equal("jw", cookieData.GetProperty("source_type").GetString());
        Assert.Equal("live_fetch", cookieData.GetProperty("data_origin").GetString());
        Assert.Equal("第8周", cookieData.GetProperty("week_label").GetString());
        var cookieSavedFiles = cookieData.GetProperty("saved_files").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(cookieSavedFiles, path => path.EndsWith("jw_schedule.csv", StringComparison.Ordinal));
        Assert.Equal(1, fakeSchedule.CookieCalls);

        var cookieLocal = await client.GetAsync($"/api/schedule/local?session_id={sessionId}&source=cookie");
        var cookieLocalData = GetData(await ReadJsonAsync(cookieLocal));
        Assert.Equal(HttpStatusCode.OK, cookieLocal.StatusCode);
        Assert.True(cookieLocalData.GetProperty("cached").GetBoolean());
        Assert.Equal("jw_schedule", cookieLocalData.GetProperty("source_key").GetString());

        fakeSchedule.FailPreferredOnce = true;
        var fallback = await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
            CookieRq = "2026-04-20",
        });
        var fallbackDoc = await ReadJsonAsync(fallback);

        Assert.Equal(HttpStatusCode.InternalServerError, fallback.StatusCode);
        Assert.False(fallbackDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("移动教务暂不可用", fallbackDoc.RootElement.GetProperty("msg").GetString());
        Assert.Equal("移动教务暂不可用", fallbackDoc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(1, fakeSchedule.CookieCalls);
    }

    [Fact]
    public async Task ScheduleEndpoints_RestoreLocalAcrossRestartAndResetLiveCookieConnection()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-restart");
        var fakeAuth = new FakeJwAuthClient();
        var fakeSchedule = new FakeJwScheduleClient();

        async Task<XkeWebHostService> CreateServiceAsync(IJwScheduleClient scheduleClient)
        {
            var service = new XkeWebHostService(new XkeWebHostOptions
            {
                HostMode = "test-schedule",
                RuntimeChannel = "dotnet8-test",
                WebRootPath = webRoot.RootPath,
                AuthDataRootPath = dataRoot.RootPath,
                JwAuthClient = fakeAuth,
                UseLegacyMobileAuthentication = true,
                UseLegacyMobileScheduleForTests = true,
                JwScheduleClient = scheduleClient,
                NoticeRemoteSource = new MissingNoticeRemoteSource(),
                Urls = new[] { "http://127.0.0.1:0" },
            });
            await service.StartAsync();
            return service;
        }

        await using var firstService = await CreateServiceAsync(fakeSchedule);
        string sessionId;
        using (var firstClient = new HttpClient { BaseAddress = new Uri(firstService.LaunchResult!.BaseUrl) })
        {
            var login = await firstClient.PostAsJsonAsync("/api/auth/login", new
            {
                user = "20260001",
                password = "secret",
                prelogin_mode = "capture",
            });
            sessionId = GetData(await ReadJsonAsync(login)).GetProperty("session_id").GetString()!;
        }

        await firstService.StopAsync();

        await using var secondService = await CreateServiceAsync(new FakeJwScheduleClient());
        using var secondClient = new HttpClient { BaseAddress = new Uri(secondService.LaunchResult!.BaseUrl) };

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var sessionResponse = await secondClient.SendAsync(sessionRequest);
        var sessionDoc = await ReadJsonAsync(sessionResponse);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.False(GetData(sessionDoc).GetProperty("connected").GetBoolean());

        using var localRequest = new HttpRequestMessage(HttpMethod.Get, "/api/schedule/local?source=preferred");
        localRequest.Headers.Add(SessionConstants.SessionHeaderName, sessionId);
        var local = await secondClient.SendAsync(localRequest);
        Assert.Equal(HttpStatusCode.OK, local.StatusCode);

        var cookieRefresh = await secondClient.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var cookieData = GetData(await ReadJsonAsync(cookieRefresh));
        Assert.Equal(HttpStatusCode.OK, cookieRefresh.StatusCode);
        Assert.Equal("jw_schedule", cookieData.GetProperty("source_key").GetString());
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_ParsesCookieFixture()
    {
        var html = ReadFixtureText(Path.Combine("fixtures", "schedule", "sample_cookie_schedule.html"));
        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
        });

        Assert.Equal("jw_schedule", result.SourceKey);
        Assert.Equal("jw", result.SourceType);
        Assert.Equal("第8周", result.WeekLabel);
        Assert.NotEmpty(result.Rows);
        Assert.Equal("高等数学", result.Rows[0]["course_name"]);
        Assert.Equal("星期一", result.Rows[0]["weekday"]);
        Assert.Equal("第1-2节", result.Rows[0]["section"]);
        Assert.NotEmpty(result.BlockTimes);
    }

    [Theory]
    [InlineData("第8周", 8)]
    [InlineData("第八周", 8)]
    [InlineData("八", 8)]
    [InlineData("第十一周", 11)]
    [InlineData("第二十周", 20)]
    public void ScheduleService_ParseWeekNumberSupportsChineseWeekLabels(string label, int expected)
    {
        Assert.Equal(expected, ScheduleService.ParseWeekNumber(label));
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_ParsesItemBoxHtml()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>上午<br/>03~04小节<br/>10:00-11:35</td>
                <td>
                  <div class="item-box">
                    <p>大学体育</p>
                    <div class="tch-name"><span>学分：1</span><span>03~04小节</span></div>
                    <span>东操场</span><span>第1-16周</span>
                    <span class="box"><p>体育</p><p>教师：王老师</p><span class="text">0304</span></span>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "8",
        });

        var row = Assert.Single(result.Rows);
        Assert.Equal("大学体育", row["course_name"]);
        Assert.Equal("星期一", row["weekday"]);
        Assert.Equal("第3-4节", row["section"]);
        Assert.Equal("王老师", row["teacher"]);
        Assert.Equal("东操场", row["room"]);
        Assert.Equal("第1-16周", row["weeks"]);
        Assert.Equal("1", row["credit"]);
        Assert.NotEmpty(result.BlockTimes);
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_ParsesSiblingHoverBoxAndCompactSections()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>第三大节<br>(05,06小节)<br/>14:00-15:40</td>
                <td>
                  <span onmouseover="kbtc(this)" class="box"><p>Web应用...</p><p>教师：程星火</p><span class="text">05~060708小节 第3周</span></span>
                  <div class="item-box">
                    <p>Web应用安全审计</p>
                    <div class="tch-name"><span>学分：4</span><span>05~060708节</span></div>
                    <div><span><img src="/jsxsd/assets_v1/images/item1.png">S1-405(新)（网络构建）</span><span><img src="/jsxsd/assets_v1/images/item3.png">第3周 星期一</span></div>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
              <tr>
                <td>第四大节<br>(07,08小节)<br/>16:00-17:40</td>
                <td>
                  <span onmouseover="kbtc(this)" class="box"><p>Web应用...</p><p>教师：程星火</p><span class="text">05~060708小节 第3周</span></span>
                  <div class="item-box">
                    <p>Web应用安全审计</p>
                    <div class="tch-name"><span>学分：4</span><span>05~060708节</span></div>
                    <div><span><img src="/jsxsd/assets_v1/images/item1.png">S1-405(新)（网络构建）</span><span><img src="/jsxsd/assets_v1/images/item3.png">第3周 星期一</span></div>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "3",
        });

        var row = Assert.Single(result.Rows);
        Assert.Equal("Web应用安全审计", row["course_name"]);
        Assert.Equal("星期一", row["weekday"]);
        Assert.Equal("第5-8节", row["section"]);
        Assert.Equal("程星火", row["teacher"]);
        Assert.Equal("S1-405(新)（网络构建）", row["room"]);
        Assert.Equal("第3周 星期一", row["weeks"]);
        Assert.Equal("4", row["credit"]);
        Assert.DoesNotContain(result.DataWarnings, warning =>
            string.Equals(warning.GetValueOrDefault("type")?.ToString(), "jw-cookie-teacher-missing", StringComparison.Ordinal));
        Assert.DoesNotContain(result.DataWarnings, warning =>
            string.Equals(warning.GetValueOrDefault("type")?.ToString(), "duplicate_slot", StringComparison.Ordinal));
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_IgnoresRemarkRowsWithoutTimeCell()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>第二大节<br/>(03,04小节)<br/>10:00-11:40</td>
                <td></td>
                <td>
                  <span class="box"><p>大学英语4</p><p>教师：谈向华</p><span class="text">03~04小节 第8周</span></span>
                  <div class="item-box">
                    <p>大学英语4</p>
                    <div class="tch-name"><span>学分：2.25</span><span>03~04节</span></div>
                    <div><span><img src="/jsxsd/assets_v1/images/item1.png">J2-106(新)</span><span><img src="/jsxsd/assets_v1/images/item3.png">第8周 星期二</span></div>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td>
              </tr>
              <tr height="30">
                <td align="center">备注&nbsp;&nbsp;</td>
                <td colspan="7" style="color: red;">&nbsp;网络安全系统集成实践     1-16周;</td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "8",
        });

        var row = Assert.Single(result.Rows);
        Assert.Equal("大学英语4", row["course_name"]);
        Assert.DoesNotContain(result.Rows, item =>
            item.GetValueOrDefault("course_name")?.ToString()?.Contains("网络安全系统集成实践", StringComparison.Ordinal) == true);
        Assert.Empty(result.DataWarnings);
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_ParsesSectionWhenWeekTextIsMixedIn()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>上午<br/>10:00-11:35</td>
                <td>
                  <div class="item-box">
                    <p>实训课程</p>
                    <div class="tch-name"><span>学分：2</span><span>星期一 / 第3-4节 / 第1-16周</span></div>
                    <span>实训楼A</span><span>第1-16周</span>
                    <span class="box"><p>实训</p><p>教师：赵老师</p></span>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "8",
        });

        var row = Assert.Single(result.Rows);
        Assert.Equal("实训课程", row["course_name"]);
        Assert.Equal("星期一", row["weekday"]);
        Assert.Equal("第3-4节", row["section"]);
        Assert.Equal("第1-16周", row["weeks"]);
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_ParsesCommaSeparatedSmallSectionsFromTimeCell()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>第一大节 (01,02小节)<br/>08:00-09:40</td>
                <td>
                  <div class="item-box">
                    <p>课程A</p>
                    <div class="tch-name"><span>学分：2</span><span>课程属性</span></div>
                    <span>教室A</span><span>第1-16周</span>
                    <span class="text">第1-16周</span>
                    <span class="box"><p>课程</p><p>教师：老师A</p></span>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "8",
        });

        var row = Assert.Single(result.Rows);
        Assert.Equal("第1-2节", row["section"]);
        Assert.Equal("08:00-09:40", row["time_range"]);
        Assert.Equal("08:00-08:45", row["small_section_1"]);
        Assert.Equal("08:55-09:40", row["small_section_2"]);
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_MergesDuplicateRoomsAndWarns()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>上午<br/>0304<br/>10:00-11:35</td>
                <td>
                  <div class="item-box">
                    <p>大学体育</p>
                    <div class="tch-name"><span>学分：1</span><span>第3-4节</span></div>
                    <span>东操场</span><span>第1-16周</span>
                    <span class="box"><p>体育</p><p>教师：王老师</p><span class="text">0304</span></span>
                  </div>
                  <div class="item-box">
                    <p>大学体育</p>
                    <div class="tch-name"><span>学分：1</span><span>03~04小节</span></div>
                    <span>西操场</span><span>第1-16周</span>
                    <span class="box"><p>体育</p><p>教师：王老师</p><span class="text">0304</span></span>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "8",
        });

        var row = Assert.Single(result.Rows);
        Assert.Equal("东操场 / 西操场", row["room"]);
        var conflict = Assert.IsType<Dictionary<string, object?>>(row["conflict"]);
        Assert.Equal("room_conflict", conflict["kind"]);
        Assert.Contains(result.DataWarnings, warning =>
            string.Equals(warning.GetValueOrDefault("type")?.ToString(), "duplicate_slot", StringComparison.Ordinal));
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_DoesNotMergeSameCourseAcrossDifferentSections()
    {
        var html = """
            <table><tbody>
              <tr>
                <td>上午<br/>0102<br/>08:00-09:35</td>
                <td>
                  <div class="item-box">
                    <p>专业实训</p>
                    <div class="tch-name"><span>学分：2</span><span>0102</span></div>
                    <span>实训楼A</span><span>第1-16周</span>
                    <span class="box"><p>实训</p><p>教师：赵老师</p><span class="text">0102</span></span>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
              <tr>
                <td>下午<br/>0304<br/>10:00-11:35</td>
                <td>
                  <div class="item-box">
                    <p>专业实训</p>
                    <div class="tch-name"><span>学分：2</span><span>0304</span></div>
                    <span>实训楼B</span><span>第1-16周</span>
                    <span class="box"><p>实训</p><p>教师：赵老师</p><span class="text">0304</span></span>
                  </div>
                </td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
              </tr>
            </tbody></table>
            """;

        var result = new TraditionalJwScheduleHtmlParser().Parse(html, new ScheduleRequest
        {
            Mode = "current",
            ManualWeek = "8",
        });

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, row => string.Equals(row["section"]?.ToString(), "第1-2节", StringComparison.Ordinal));
        Assert.Contains(result.Rows, row => string.Equals(row["section"]?.ToString(), "第3-4节", StringComparison.Ordinal));
        Assert.DoesNotContain(result.DataWarnings, warning =>
            string.Equals(warning.GetValueOrDefault("type")?.ToString(), "duplicate_slot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScheduleEndpoints_CookieRowsAreNormalizedBeforeCache()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-cookie-normalize");
        var fakeSchedule = new FakeJwScheduleClient
        {
            IncludeMalformedCookieRows = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = GetData(await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        }))).GetProperty("session_id").GetString()!;

        var refresh = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var data = GetData(await ReadJsonAsync(refresh));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var row = Assert.Single(data.GetProperty("rows").EnumerateArray());
        Assert.Equal("星期一", row.GetProperty("weekday").GetString());
        Assert.Equal("第1-2节", row.GetProperty("section").GetString());
        Assert.Equal("线性代数", row.GetProperty("course_name").GetString());
        Assert.Equal("1-202 / 超星网络教学平台2", row.GetProperty("room").GetString());
        Assert.Equal("room_conflict", row.GetProperty("conflict").GetProperty("kind").GetString());
        Assert.Contains(data.GetProperty("data_warnings").EnumerateArray(), warning =>
            warning.GetProperty("type").GetString() == "schedule-row-invalid");
        var duplicateWarnings = data.GetProperty("data_warnings")
            .EnumerateArray()
            .Where(warning => warning.GetProperty("type").GetString() == "duplicate_slot")
            .ToArray();
        var duplicateWarning = Assert.Single(duplicateWarnings);
        var discardedRooms = duplicateWarning.GetProperty("discarded")
            .EnumerateArray()
            .Select(item => item.GetProperty("room").GetString())
            .ToArray();
        Assert.Contains("手工导入平台", discardedRooms);
        Assert.Contains("超星网络教学平台2", discardedRooms);

        var local = await client.GetAsync($"/api/schedule/local?session_id={sessionId}&source=cookie");
        var localData = GetData(await ReadJsonAsync(local));
        Assert.Single(localData.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task ScheduleEndpoints_CaptchaRequiredReturnsChallengeAndSubmitContinues()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-captcha");
        var fakeSchedule = new FakeJwScheduleClient
        {
            RequireCaptchaOnce = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = GetData(await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        }))).GetProperty("session_id").GetString()!;

        var challenge = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var challengeData = GetData(await ReadJsonAsync(challenge));

        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        Assert.Equal("captcha_required", challengeData.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(challengeData.GetProperty("challenge_id").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(challengeData.GetProperty("captcha_image").GetString()));

        var submit = await client.PostAsJsonAsync($"/api/auth/jw-captcha/submit?session_id={sessionId}", new JwCaptchaSubmitRequest
        {
            ChallengeId = challengeData.GetProperty("challenge_id").GetString()!,
            CaptchaCode = "2468",
        });
        var submitData = GetData(await ReadJsonAsync(submit));

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal("jw_schedule", submitData.GetProperty("source_key").GetString());
        Assert.Equal("live_fetch", submitData.GetProperty("data_origin").GetString());
    }

    [Fact]
    public async Task ScheduleEndpoints_CaptchaSubmitPreservesContinuationStatusCode()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-captcha-status");
        var fakeSchedule = new FakeJwScheduleClient
        {
            RequireCaptchaOnce = true,
            CaptchaContinuationFailureStatusCode = 409,
            CaptchaContinuationFailureMessage = "教务系统登录上下文已失效",
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = GetData(await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        }))).GetProperty("session_id").GetString()!;

        var challenge = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var challengeData = GetData(await ReadJsonAsync(challenge));

        var submit = await client.PostAsJsonAsync($"/api/auth/jw-captcha/submit?session_id={sessionId}", new JwCaptchaSubmitRequest
        {
            ChallengeId = challengeData.GetProperty("challenge_id").GetString()!,
            CaptchaCode = "2468",
        });
        var body = await submit.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Contains("教务系统登录上下文已失效", body);
    }

    [Fact]
    public async Task ScheduleEndpoints_CaptchaSubmitFailureCanRetrySameChallenge()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-captcha-retry");
        var fakeSchedule = new FakeJwScheduleClient
        {
            RequireCaptchaOnce = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = GetData(await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        }))).GetProperty("session_id").GetString()!;

        var challenge = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var challengeData = GetData(await ReadJsonAsync(challenge));
        var challengeId = challengeData.GetProperty("challenge_id").GetString()!;

        var emptySubmit = await client.PostAsJsonAsync($"/api/auth/jw-captcha/submit?session_id={sessionId}", new JwCaptchaSubmitRequest
        {
            ChallengeId = challengeId,
            CaptchaCode = string.Empty,
        });
        Assert.Equal(HttpStatusCode.BadRequest, emptySubmit.StatusCode);

        var retrySubmit = await client.PostAsJsonAsync($"/api/auth/jw-captcha/submit?session_id={sessionId}", new JwCaptchaSubmitRequest
        {
            ChallengeId = challengeId,
            CaptchaCode = "2468",
        });
        var retryData = GetData(await ReadJsonAsync(retrySubmit));

        Assert.Equal(HttpStatusCode.OK, retrySubmit.StatusCode);
        Assert.Equal("jw_schedule", retryData.GetProperty("source_key").GetString());
    }

    [Fact]
    public async Task BlockedStudent_ScheduleEndpointReturns403Detail()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-blocked");
        var fixture = CreateBlockedNoticeFixture("20269999");
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20269999",
            Password = "secret",
            StudentId = "20269999",
            TrustedLoginToken = "trusted-token",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        root.ForStudent("20269999").SaveProfile(new StudentProfileDocument
        {
            StudentId = "20269999",
            Name = "受限学生",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        var session = root.Sessions().Create("20269999", "20269999", "capture");

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: new FakeJwScheduleClient(),
            noticeRemoteSource: fixture.Source,
            noticeSigningPublicKeyPem: fixture.PublicKeyPem);
        var client = host.Client;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/schedule/local?source=preferred");
        request.Headers.Add(SessionConstants.SessionHeaderName, session.SessionId);
        var response = await client.SendAsync(request);
        var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("detail").GetProperty("blocked").GetBoolean());
        Assert.Equal("20269999", document.RootElement.GetProperty("detail").GetProperty("student_id").GetString());
    }

    [Fact]
    public async Task ScheduleEndpoints_ExposeWarningsFromNormalizedMobilePayload()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-warnings");
        var fakeSchedule = new FakeJwScheduleClient
        {
            IncludeMissingSectionWarning = true,
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwAuthClient: new FakeJwAuthClient(),
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = GetData(await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/login", new
        {
            user = "20260001",
            password = "secret",
            prelogin_mode = "capture",
        }))).GetProperty("session_id").GetString()!;

        var refreshData = GetData(await ReadJsonAsync(await client.PostAsJsonAsync($"/api/schedule/preferred?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        })));

        Assert.Equal(1, refreshData.GetProperty("data_warnings").GetArrayLength());
        Assert.Equal("mobile-course-section-missing", refreshData.GetProperty("data_warnings")[0].GetProperty("type").GetString());
    }

    private static JsonElement GetData(JsonDocument document)
    {
        return document.RootElement.GetProperty("data");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    private static string ReadFixtureText(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fixture not found: {relativePath}");
    }

    private static NoticeFixture CreateBlockedNoticeFixture(string blockedStudentId)
    {
        const string updatedAt = "2026-04-23T12:00:00Z";
        var noticeDirectory = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["manifests"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "beta",
                    ["path"] = "notice/beta.json",
                    ["updated_at"] = updatedAt,
                    ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(new Dictionary<string, object?>
                    {
                        ["schema_version"] = "1",
                        ["manifest_id"] = "beta",
                        ["updated_at"] = updatedAt,
                        ["bundles"] = new List<object?>(),
                    })),
                },
            },
        };

        var manifest = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["manifest_id"] = "beta",
            ["updated_at"] = updatedAt,
            ["bundles"] = new List<object?>(),
        };

        var versionPolicy = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["versions"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "beta",
                    ["version"] = "1.0.0",
                    ["enabled"] = true,
                    ["updated_at"] = updatedAt,
                    ["app_control"] = new Dictionary<string, object?>
                    {
                        ["state"] = "normal",
                        ["reason"] = string.Empty,
                    },
                    ["feature_flags"] = new Dictionary<string, object?>(),
                    ["feature_limits"] = new Dictionary<string, object?>(),
                    ["runtime_policy"] = new Dictionary<string, object?>(),
                    ["telemetry_enabled"] = false,
                },
            },
        };

        var updateInfo = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["latest_version"] = "1.0.0",
        };

        var accessPolicy = new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["updated_at"] = updatedAt,
            ["default_message"] = "该账号当前不可用",
            ["blocked_students"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["student_id_hash"] = NoticeHash.Sha256Hex(Encoding.UTF8.GetBytes(blockedStudentId)),
                    ["message"] = "该账号当前不可用",
                },
            },
        };

        var index = new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["updated_at"] = updatedAt,
            ["notice"] = new Dictionary<string, object?>
            {
                ["path"] = "notice/directory.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(noticeDirectory)),
            },
            ["version_policy"] = new Dictionary<string, object?>
            {
                ["path"] = "config/version_policy.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(versionPolicy)),
            },
            ["update_info"] = new Dictionary<string, object?>
            {
                ["path"] = "config/update_info.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(updateInfo)),
            },
            ["access_policy"] = new Dictionary<string, object?>
            {
                ["path"] = "config/access_policy.json",
                ["updated_at"] = updatedAt,
                ["sha256"] = NoticeHash.Sha256Hex(NoticeCanonicalJson.Serialize(accessPolicy)),
            },
        };

        var cloudFeed = new Dictionary<string, object?>
        {
            ["schema_version"] = "1",
            ["updated_at"] = updatedAt,
            ["announcement"] = new Dictionary<string, object?>
            {
                ["items"] = new List<object?>(),
            },
            ["policy"] = new Dictionary<string, object?>
            {
                ["version_policy"] = versionPolicy,
                ["access_policy"] = accessPolicy,
            },
            ["update_info"] = updateInfo,
        };

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cloud-feed.json"] = SerializeJson(cloudFeed),
            ["index.json"] = SerializeJson(index),
            ["index.sig"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(ecdsa.SignData(NoticeCanonicalJson.Serialize(index), HashAlgorithmName.SHA512)).TrimEnd('=')),
            ["notice/directory.json"] = SerializeJson(noticeDirectory),
            ["notice/beta.json"] = SerializeJson(manifest),
            ["config/version_policy.json"] = SerializeJson(versionPolicy),
            ["config/update_info.json"] = SerializeJson(updateInfo),
            ["config/access_policy.json"] = SerializeJson(accessPolicy),
        };

        return new NoticeFixture(new MemoryNoticeRemoteSource(files), ecdsa.ExportSubjectPublicKeyInfoPem());
    }

    private static byte[] SerializeJson(object payload)
    {
        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    private sealed record NoticeFixture(MemoryNoticeRemoteSource Source, string PublicKeyPem);

    private sealed class FakeJwAuthClient : IJwAuthClient
    {
        public Task<JwAuthResult> InitializeAsync(string user, string password, string preloginMode, CancellationToken cancellationToken = default, MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            return Task.FromResult(new JwAuthResult(BuildProfile(user), "mobile-token-initial"));
        }

        public Task<JwAuthResult> ConnectAsync(string user, string password, string preloginMode, CancellationToken cancellationToken = default, MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            return Task.FromResult(new JwAuthResult(BuildProfile(user), "mobile-token-connected"));
        }

        private static StudentProfileDocument BuildProfile(string user)
        {
            return new StudentProfileDocument
            {
                StudentId = user,
                Name = "测试同学",
                ClassName = "软件一班",
                MajorName = "软件技术",
                AcademyName = "信息工程学院",
                TrainingLevel = "专科",
                CampusName = "主校区",
                UpdatedAt = "2026-04-23T12:00:00Z",
            };
        }
    }

    private sealed class FakeJwScheduleClient : IJwScheduleClient
    {
        public bool FailPreferredOnce { get; set; }
        public bool IncludeMissingSectionWarning { get; set; }
        public bool IncludeMalformedCookieRows { get; set; }
        public bool RequireCaptchaOnce { get; set; }
        public int? CaptchaContinuationFailureStatusCode { get; set; }
        public string CaptchaContinuationFailureMessage { get; set; } = "验证码续跑失败";
        public int PreferredCurrentWeekNumber { get; set; } = 7;
        public int? PreferredViewedWeekNumber { get; set; }
        public string PreferredWeekLabelOverride { get; set; } = string.Empty;
        public int PreferredCalls { get; private set; }
        public int CookieCalls { get; private set; }
        public string LastExistingToken { get; private set; } = string.Empty;

        public Task<JwScheduleFetchResult> FetchPreferredAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null)
        {
            PreferredCalls++;
            LastExistingToken = existingToken;
            if (FailPreferredOnce)
            {
                FailPreferredOnce = false;
                throw new JwScheduleException("移动教务暂不可用");
            }

            return Task.FromResult(BuildResult(
                "mobile_schedule",
                "移动教务课表",
                "mobile",
                PreferredCurrentWeekNumber,
                "移动教务课表刷新成功",
                IncludeMissingSectionWarning,
                PreferredViewedWeekNumber,
                PreferredWeekLabelOverride));
        }

        public Task<JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string existingToken = "", MobileJwEncryptedCredentialDocument? mobileJw = null, string sessionId = "")
        {
            CookieCalls++;
            if (RequireCaptchaOnce)
            {
                RequireCaptchaOnce = false;
                throw new JwCaptchaRequiredException(
                    "需要输入教务系统验证码",
                    "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxODAiIGhlaWdodD0iNjQiPjx0ZXh0IHg9IjIwIiB5PSIzNiI+SldURVNUPC90ZXh0Pjwvc3ZnPg==",
                    expiresInSeconds: 120,
                    continueAsync: (captchaCode, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (!string.Equals(captchaCode, "2468", StringComparison.Ordinal))
                        {
                            throw new JwScheduleException("验证码错误");
                        }

                        if (CaptchaContinuationFailureStatusCode is int statusCode)
                        {
                            throw new JwScheduleException(CaptchaContinuationFailureMessage, statusCode);
                        }

                        return Task.FromResult(BuildResult("jw_schedule", "教务系统课表", "jw", 8, "Cookie 课表刷新成功"));
                    });
            }

            return Task.FromResult(IncludeMalformedCookieRows
                ? BuildMalformedCookieResult()
                : BuildResult("jw_schedule", "教务系统课表", "jw", 8, "Cookie 课表刷新成功"));
        }

        private static JwScheduleFetchResult BuildMalformedCookieResult()
        {
            var weekLabel = "第8周";
            return new JwScheduleFetchResult(
                SourceKey: "jw_schedule",
                SourceLabel: "教务系统课表",
                SourceType: "jw",
                WeekLabel: weekLabel,
                CurrentWeekNumber: 8,
                ViewedWeekNumber: 8,
                AvailableWeeks:
                [
                    new Dictionary<string, object?>
                    {
                        ["value"] = "8",
                        ["label"] = weekLabel,
                        ["selected"] = true,
                    },
                ],
                WeekDates: new Dictionary<string, string>(),
                BlockTimes: [],
                DataWarnings:
                [
                    new Dictionary<string, object?>
                    {
                        ["type"] = "duplicate_slot",
                        ["course_name"] = "线性代数",
                        ["weekday"] = "星期一",
                        ["section"] = "第1-2节",
                        ["kept_room"] = "1-202",
                        ["discarded"] = new List<object?>
                        {
                            new Dictionary<string, object?>
                            {
                                ["room"] = "手工导入平台",
                            },
                        },
                        ["message"] = "上游已提示同时间多教室",
                    },
                ],
                Rows:
                [
                    new Dictionary<string, object?>
                    {
                        ["source"] = "Cookie",
                        ["week_label"] = weekLabel,
                        ["weekday"] = "周一",
                        ["class_time"] = "08:00-09:35",
                        ["time_text"] = "星期一 / 第1-16周 / 第1-2节 / 08:00-09:35",
                        ["course_name"] = "线性代数",
                        ["teacher"] = "教师：李老师",
                        ["room"] = "1-202",
                    },
                    new Dictionary<string, object?>
                    {
                        ["source"] = "Cookie",
                        ["week_label"] = weekLabel,
                        ["weekday"] = "星期一",
                        ["section"] = "第1-2节",
                        ["course_name"] = "线性代数",
                        ["teacher"] = "李老师",
                        ["room"] = "超星网络教学平台2",
                    },
                    new Dictionary<string, object?>
                    {
                        ["source"] = "Cookie",
                        ["week_label"] = weekLabel,
                        ["weekday"] = "星期二",
                        ["course_name"] = "缺节次课程",
                    },
                    new Dictionary<string, object?>
                    {
                        ["source"] = "Cookie",
                        ["week_label"] = weekLabel,
                        ["weekday"] = "星期三",
                        ["section"] = "第1-16周",
                        ["course_name"] = "周次误识别课程",
                    },
                ],
                RawPayload: new Dictionary<string, object?>
                {
                    ["source"] = "jw",
                },
                Remarks: "Cookie 课表刷新成功",
                FallbackReason: string.Empty);
        }

        private static JwScheduleFetchResult BuildResult(string sourceKey, string sourceLabel, string sourceType, int weekNumber, string remarks, bool includeWarning = false, int? viewedWeekNumber = null, string weekLabelOverride = "")
        {
            var resolvedViewedWeekNumber = viewedWeekNumber ?? weekNumber;
            var weekLabel = string.IsNullOrWhiteSpace(weekLabelOverride)
                ? $"第{resolvedViewedWeekNumber}周"
                : weekLabelOverride;
            return new JwScheduleFetchResult(
                SourceKey: sourceKey,
                SourceLabel: sourceLabel,
                SourceType: sourceType,
                WeekLabel: weekLabel,
                CurrentWeekNumber: weekNumber,
                ViewedWeekNumber: resolvedViewedWeekNumber,
                AvailableWeeks:
                [
                    new Dictionary<string, object?>
                    {
                        ["value"] = resolvedViewedWeekNumber.ToString(),
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
                DataWarnings: includeWarning
                    ? [
                        new Dictionary<string, object?>
                        {
                            ["type"] = "mobile-course-section-missing",
                            ["message"] = "高等数学 缺少节次字段",
                        },
                    ]
                    : [
                        new Dictionary<string, object?>
                        {
                            ["type"] = "duplicate_slot",
                            ["course_name"] = "高等数学",
                            ["week_label"] = weekLabel,
                            ["weekday"] = "星期一",
                            ["kept_room"] = "1-101",
                            ["kept_weeks"] = weekLabel,
                            ["discarded"] = new List<object?>(),
                            ["message"] = "检测到冲突",
                        },
                    ],
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
                        ["block_time"] = string.Empty,
                        ["small_section_1"] = string.Empty,
                        ["small_section_2"] = string.Empty,
                        ["break_minutes"] = 0,
                        ["course_name"] = "高等数学",
                        ["teacher"] = "张老师",
                        ["room"] = "1-101",
                        ["weeks"] = weekLabel,
                        ["credit"] = "4",
                    },
                ],
                RawPayload: new Dictionary<string, object?>
                {
                    ["source"] = sourceType,
                    ["week"] = weekNumber,
                    ["viewed_week"] = resolvedViewedWeekNumber,
                },
                Remarks: remarks,
                FallbackReason: string.Empty);
        }
    }

}
