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
using Xke.Core.Schedule;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleWebHostTests
{
    [Fact]
    public async Task ScheduleEndpoints_RestoreLocalAcrossRestartAndResetLiveCookieConnection()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-restart");
        var fakeSchedule = new FakeJwScheduleClient();
        var handler = new ProtocolHandler();

        async Task<XkeWebHostService> CreateServiceAsync(IJwScheduleClient scheduleClient)
        {
            var service = new XkeWebHostService(new XkeWebHostOptions
            {
                HostMode = "test-schedule",
                RuntimeChannel = "dotnet8-test",
                WebRootPath = webRoot.RootPath,
                ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(),
                AuthDataRootPath = dataRoot.RootPath,
                JwScheduleClient = scheduleClient,
                JwLoginProtocolFactory = (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)),
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
            var loginDoc = await ReadJsonAsync(login);
            var challengeId = GetData(loginDoc).GetProperty("challenge_id").GetString();
            var submit = await firstClient.PostAsJsonAsync("/api/auth/captcha", new
            {
                challenge_id = challengeId,
                captcha_code = "1234",
            });
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            sessionId = GetData(await ReadJsonAsync(submit)).GetProperty("session_id").GetString()!;
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
        var html = """
            <html>
              <body>
                <div class="schedule-title">第8周</div>
                <table class="kbtable">
                  <tr>
                    <td>上午<br />第1-2节<br />08:00-09:35</td>
                    <td>
                      <div class="kbcontent">
                        高等数学<br />
                        <font title="教师">张老师</font>
                        <font title="教室">1-101</font>
                        <font title="周次(节次)">8(周)[01-02节]</font>
                      </div>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
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
        Assert.Equal(string.Empty, row["small_section_1"]);
        Assert.Equal(string.Empty, row["small_section_2"]);
    }

    [Fact]
    public void TraditionalJwScheduleHtmlParser_PreservesDistinctLocations()
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

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, row => row["room"]?.ToString() == "东操场");
        Assert.Contains(result.Rows, row => row["room"]?.ToString() == "西操场");
        Assert.DoesNotContain(result.DataWarnings, warning => warning.GetValueOrDefault("type")?.ToString() == "duplicate_slot");
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
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");

        var refresh = await client.PostAsJsonAsync($"/api/schedule/cookie?session_id={sessionId}", new ScheduleRequest
        {
            Mode = "current",
        });
        var data = GetData(await ReadJsonAsync(refresh));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var normalizedRows = data.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(3, normalizedRows.Length);
        var row = normalizedRows[0];
        Assert.Equal("星期一", row.GetProperty("weekday").GetString());
        Assert.Equal("第1-2节", row.GetProperty("section").GetString());
        Assert.Equal("线性代数", row.GetProperty("course_name").GetString());
        Assert.Equal("1-202", row.GetProperty("room").GetString());
        Assert.Equal("超星网络教学平台2", normalizedRows[1].GetProperty("room").GetString());
        Assert.Equal("1-202", normalizedRows[2].GetProperty("room").GetString());
        Assert.Equal(Enumerable.Range(1, 8), row.GetProperty("week_numbers").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(Enumerable.Range(9, 8), normalizedRows[2].GetProperty("week_numbers").EnumerateArray().Select(value => value.GetInt32()));
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
        Assert.DoesNotContain("超星网络教学平台2", discardedRooms);

        var local = await client.GetAsync($"/api/schedule/local?session_id={sessionId}&source=cookie");
        var localData = GetData(await ReadJsonAsync(local));
        Assert.Equal(3, localData.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task ScheduleEndpoints_CaptchaRequiredReturnsChallengeAndSubmitContinues()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-schedule-webhost-captcha");
        var fakeSchedule = new FakeJwScheduleClient
        {
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");
        fakeSchedule.RequireCaptchaOnce = true;

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
            CaptchaContinuationFailureStatusCode = 409,
            CaptchaContinuationFailureMessage = "教务系统登录上下文已失效",
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");
        fakeSchedule.RequireCaptchaOnce = true;

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
        };

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "test-schedule",
            jwScheduleClient: fakeSchedule);
        var client = host.Client;
        var sessionId = await LoginAsync(client, "20260001");
        fakeSchedule.RequireCaptchaOnce = true;

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

    private static async Task<string> LoginAsync(HttpClient client, string user)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user,
            password = "secret",
            prelogin_mode = "capture",
        });
        var document = await ReadJsonAsync(login);
        var challengeId = GetData(document).GetProperty("challenge_id").GetString()!;
        var submit = await client.PostAsJsonAsync("/api/auth/captcha", new
        {
            challenge_id = challengeId,
            captcha_code = "1234",
        });
        return GetData(await ReadJsonAsync(submit)).GetProperty("session_id").GetString()!;
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

    private sealed class FakeJwScheduleClient : IJwScheduleClient
    {
        public bool IncludeMalformedCookieRows { get; set; }
        public bool RequireCaptchaOnce { get; set; }
        public int? CaptchaContinuationFailureStatusCode { get; set; }
        public string CaptchaContinuationFailureMessage { get; set; } = "验证码续跑失败";
        public int CookieCalls { get; private set; }

        public Task<JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode, ScheduleRequest request, CancellationToken cancellationToken = default, string sessionId = "")
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
                        ["weeks"] = "1-8周",
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
                        ["source"] = "Cookie", ["week_label"] = weekLabel, ["weekday"] = "星期一",
                        ["section"] = "第1-2节", ["course_name"] = "线性代数", ["teacher"] = "李老师",
                        ["room"] = "1-202", ["weeks"] = "9-16周",
                    },
                    new Dictionary<string, object?>
                    {
                        ["source"] = "Cookie", ["week_label"] = weekLabel, ["weekday"] = "星期一",
                        ["section"] = "第1-2节", ["course_name"] = "线性代数", ["teacher"] = "李老师",
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
