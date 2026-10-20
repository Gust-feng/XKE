using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class TraditionalJwClientTests
{
    [Fact]
    public async Task NativeJwScheduleClient_BackgroundRefreshNeverStartsCaptchaWithoutStoredSession()
    {
        using var data = new TemporaryDirectory();
        var handler = new TraditionalJwFakeHandler();
        using var http = new HttpClient(handler);
        using var sessions = new TraditionalJwSessionRuntimeStore();
        var client = new NativeJwScheduleClient(sessions, new XkeDataRoot(data.RootPath));

        var error = await Assert.ThrowsAsync<JwScheduleException>(() => client.FetchExistingSessionAsync(
            "20260001", "test-password", "auto", new ScheduleRequest { Mode = "current" }, "session", default));

        Assert.Equal(401, error.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TraditionalJwClient_AutoManualCaptchaCompletesBootstrapAndFetchesScheduleWithoutCapture()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-data");
        var handler = new TraditionalJwFakeHandler();
        using var http = new HttpClient(handler);
        var options = new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
        };
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", options, http);

        var challenge = await client.BeginLoginAsync();
        var result = await client.CompleteLoginAndFetchScheduleAsync(
            "2468",
            new ScheduleRequest
            {
                CookieRq = "2026-04-20",
            },
            new TraditionalJwScheduleHtmlParser());

        Assert.StartsWith("data:image/svg+xml;base64,", challenge.CaptchaImage);
        Assert.Equal("jw_schedule", result.SourceKey);
        Assert.Equal("jw", result.SourceType);
        Assert.Equal("第8周", result.WeekLabel);
        Assert.NotEmpty(result.Rows);
        Assert.Contains(handler.Requests, request => request.Uri.Host == "jw.hnsoftedu.com"
                                                     && request.Uri.AbsolutePath.EndsWith("mainV_index_loadkb.jsp", StringComparison.Ordinal)
                                                     && request.Uri.Query.Contains("rq=2026-04-20", StringComparison.Ordinal));
        var loginPost = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post && request.Uri.Host == "sso.hnsoftedu.com" && request.Uri.AbsolutePath == "/login");
        Assert.Equal(string.Empty, loginPost.Uri.Query);
        Assert.Equal(2, Regex.Matches(loginPost.Body, "captcha_code=").Count);
        Assert.Contains("captcha_code=2468", loginPost.Body);
        Assert.Contains("password=", loginPost.Body);
        Assert.DoesNotContain("secret-pass", loginPost.Body, StringComparison.Ordinal);
        Assert.Contains("Mozilla/5.0", loginPost.Headers["User-Agent"]);
        Assert.Contains("zh-CN", loginPost.Headers["Accept-Language"]);
        Assert.Contains("zh", loginPost.Headers["Accept-Language"]);
        Assert.Equal("1", loginPost.Headers["DNT"]);

        var preloginPost = Assert.Single(handler.Requests.Where(request =>
            request.Method == HttpMethod.Post
            && request.Uri.Host == "sso.hnsoftedu.com"
            && request.Uri.AbsolutePath == "/linkid/protected/api/dictconfig/get").Take(1));
        Assert.Equal("application/json", preloginPost.ContentHeaders["Content-Type"]);
        Assert.DoesNotContain("text/plain", preloginPost.ContentHeaders["Content-Type"], StringComparison.OrdinalIgnoreCase);
        var historicalCapturePath = Path.Combine(
            options.DataRootPath,
            "students",
            XkeDataRoot.SanitizePathSegment("20260001"),
            "prelogin_capture.xml");
        Assert.False(File.Exists(historicalCapturePath));
    }

    [Fact]
    public async Task TraditionalJwClient_ManualWeekSelectsMatchingTraditionalJwOption()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-manual-week");
        var handler = new TraditionalJwFakeHandler();
        using var http = new HttpClient(handler);
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
        }, http);

        await client.BeginLoginAsync();
        var result = await client.CompleteLoginAndFetchScheduleAsync(
            "2468",
            new ScheduleRequest
            {
                Mode = "manual",
                ManualWeek = "九",
            },
            new TraditionalJwScheduleHtmlParser());

        Assert.Equal("第9周", result.WeekLabel);
        Assert.Equal(8, result.CurrentWeekNumber);
        Assert.Equal(9, result.ViewedWeekNumber);
        Assert.Contains(handler.Requests, request => request.Uri.Host == "jw.hnsoftedu.com"
                                                     && request.Uri.AbsolutePath.EndsWith("mainV_index_loadkb.jsp", StringComparison.Ordinal)
                                                     && request.Uri.Query.Contains("rq=2026-04-27", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TraditionalJwClient_CookieRqSelectsExactTraditionalJwWeek()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-cookie-rq-week");
        var handler = new TraditionalJwFakeHandler();
        using var http = new HttpClient(handler);
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
        }, http);

        await client.BeginLoginAsync();
        var result = await client.CompleteLoginAndFetchScheduleAsync(
            "2468",
            new ScheduleRequest
            {
                Mode = "current",
                CookieRq = "2026-04-27",
            },
            new TraditionalJwScheduleHtmlParser());

        Assert.Equal("第9周", result.WeekLabel);
        Assert.Equal(8, result.CurrentWeekNumber);
        Assert.Equal(9, result.ViewedWeekNumber);
        Assert.Contains(handler.Requests, request => request.Uri.Host == "jw.hnsoftedu.com"
                                                     && request.Uri.AbsolutePath.EndsWith("mainV_index_loadkb.jsp", StringComparison.Ordinal)
                                                     && request.Uri.Query.Contains("rq=2026-04-27", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TraditionalJwClient_AutoPreloginFailureReturnsReadableConflict()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-auto-prelogin-failure");
        var handler = new TraditionalJwFakeHandler
        {
            FailPreloginRequests = true,
        };
        using var http = new HttpClient(handler);
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
        }, http);

        var error = await Assert.ThrowsAsync<JwScheduleException>(() => client.BeginLoginAsync());

        Assert.Equal(409, error.StatusCode);
        Assert.Contains("prelogin_failed", error.Message);
    }

    [Fact]
    public async Task TraditionalJwClient_RejectsBootstrapWithoutJsxsdSessionCookie()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-no-jsession");
        var handler = new TraditionalJwFakeHandler
        {
            OmitJwSessionCookie = true,
        };
        using var http = new HttpClient(handler);
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
        }, http);

        await client.BeginLoginAsync();
        var error = await Assert.ThrowsAsync<JwScheduleException>(() => client.CompleteLoginAndFetchScheduleAsync(
            "2468",
            new ScheduleRequest(),
            new TraditionalJwScheduleHtmlParser()));

        Assert.Contains("/jsxsd", error.Message);
    }

    [Fact]
    public async Task TraditionalJwClient_LoginFailureIncludesSafeStageDiagnostics()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-login-failure");
        var handler = new TraditionalJwFakeHandler
        {
            LoginPostWithoutTicket = true,
        };
        using var http = new HttpClient(handler);
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
        }, http);

        await client.BeginLoginAsync();
        var error = await Assert.ThrowsAsync<JwScheduleException>(() => client.CompleteLoginAndFetchScheduleAsync(
            "2468",
            new ScheduleRequest(),
            new TraditionalJwScheduleHtmlParser()));

        Assert.Equal(400, error.StatusCode);
        Assert.Contains("阶段=portal_login", error.Message);
        Assert.Contains("HTTP=200", error.Message);
        Assert.DoesNotContain("secret-pass", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TraditionalJwClient_ParsesProfileFromTheAuthenticatedJwSession()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-profile");
        using var http = new HttpClient(new TraditionalJwFakeHandler());
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions { DataRootPath = dataRoot.RootPath }, http);

        await client.BeginLoginAsync();
        await client.CompleteLoginAndFetchScheduleAsync("2468", new ScheduleRequest(), new TraditionalJwScheduleHtmlParser());
        var profile = await client.FetchStudentProfileAsync();

        Assert.Equal("20260001", profile.StudentId);
        Assert.Equal("测试同学", profile.Name);
        Assert.Equal("信息工程学院", profile.AcademyName);
        Assert.Equal("软件技术", profile.MajorName);
    }

    [Theory]
    [InlineData("portal_login")]
    [InlineData("jw_ticket")]
    [InlineData("jw_redirect")]
    [InlineData("jw_schedule_params")]
    public async Task TraditionalJwClient_TimeoutsIncludeSafeStageDiagnostics(string delayStage)
    {
        using var dataRoot = new TemporaryDirectory($"xke-traditional-jw-timeout-{delayStage}");
        var handler = new TraditionalJwFakeHandler
        {
            DelayStage = delayStage,
        };
        using var http = new HttpClient(handler);
        using var client = new TraditionalJwClient("20260001", "secret-pass", "auto", new TraditionalJwOptions
        {
            DataRootPath = dataRoot.RootPath,
            Timeout = TimeSpan.FromMilliseconds(50),
        }, http);

        await client.BeginLoginAsync();
        var error = await Assert.ThrowsAsync<JwScheduleException>(() => client.CompleteLoginAndFetchScheduleAsync(
            "2468",
            new ScheduleRequest
            {
                CookieRq = "2026-04-20",
            },
            new TraditionalJwScheduleHtmlParser()));

        Assert.Equal(504, error.StatusCode);
        Assert.Contains($"阶段={delayStage}", error.Message);
        Assert.Contains("timeout=0.1s", error.Message);
        Assert.DoesNotContain("HttpClient.Timeout", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-pass", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeJwScheduleClient_ReusesBootstrappedTraditionalSessionUntilExpired()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-native-reuse");
        var handler = new TraditionalJwFakeHandler();
        using var traditionalHttp = new HttpClient(handler);
        var runtimeStore = new TraditionalJwSessionRuntimeStore();
        var factoryCalls = 0;
        var native = new NativeJwScheduleClient(
            new TraditionalJwScheduleHtmlParser(),
            runtimeStore,
            (user, password, preloginMode) =>
            {
                factoryCalls++;
                return new TraditionalJwClient(user, password, preloginMode, new TraditionalJwOptions
                {
                    DataRootPath = dataRoot.RootPath,
                }, traditionalHttp);
            },
            (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)));

        var challenge = await Assert.ThrowsAsync<JwCaptchaRequiredException>(() => native.FetchCookieAsync(
            "20260001",
            "secret-pass",
            "auto",
            new ScheduleRequest
            {
                CookieRq = "2026-04-20",
            },
            sessionId: "session-1"));
        var first = await challenge.ContinueAsync!("2468", CancellationToken.None);
        var second = await native.FetchCookieAsync(
            "20260001",
            "secret-pass",
            "auto",
            new ScheduleRequest
            {
                CookieRq = "2026-04-20",
            },
            sessionId: "session-1");

        Assert.Equal("jw_schedule", first.SourceKey);
        Assert.Equal("jw_schedule", second.SourceKey);
        Assert.Equal(0, factoryCalls);
        Assert.Equal(1, handler.CaptchaRequests);
        Assert.Equal(1, handler.DirectLoginPosts);
        Assert.Equal(2, handler.ScheduleLoadRequests);
    }

    [Fact]
    public async Task NativeJwScheduleClient_RestoresPersistedSessionBeforeRequestingCaptcha()
    {
        using var dataRoot = new TemporaryDirectory("xke-traditional-jw-persisted-session");
        var root = new Xke.Storage.XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        root.JwSessions().Save(new Xke.Contracts.Models.JwSessionDocument
        {
            User = "20260001",
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
            Cookies =
            [
                new Xke.Contracts.Models.JwSessionCookieDocument { Name = "JSESSIONID", Value = "test-session", Host = "jw.hnsoftedu.com", Path = "/jsxsd" },
            ],
        });
        var handler = new TraditionalJwFakeHandler();
        using var traditionalHttp = new HttpClient(handler);
        var native = new NativeJwScheduleClient(
            new TraditionalJwScheduleHtmlParser(),
            new TraditionalJwSessionRuntimeStore(),
            (user, password, preloginMode) => new TraditionalJwClient(user, password, preloginMode, TraditionalJwOptions.FromDataRoot(root), traditionalHttp),
            directLoginFactory: (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler)),
            dataRoot: root);

        var result = await native.FetchCookieAsync("20260001", "secret-pass", "auto", new ScheduleRequest { Mode = "current" }, sessionId: "session-1");

        Assert.Equal("jw_schedule", result.SourceKey);
        Assert.Equal(0, handler.CaptchaRequests);
    }

    [Fact]
    public void TraditionalJwClient_ExportsAllCookiesFromApprovedJwHosts()
    {
        var document = new Xke.Contracts.Models.JwSessionDocument
        {
            User = "20260001",
            Cookies =
            [
                new Xke.Contracts.Models.JwSessionCookieDocument { Name = "JSESSIONID", Value = "session", Host = "jw.hnsoftedu.com", Path = "/jsxsd" },
                new Xke.Contracts.Models.JwSessionCookieDocument { Name = "SERVERID", Value = "server", Host = "222.243.161.213", Path = "/" },
                new Xke.Contracts.Models.JwSessionCookieDocument { Name = "route", Value = "route-cookie", Host = "jw.hnsoftedu.com", Path = "/" },
                new Xke.Contracts.Models.JwSessionCookieDocument { Name = "third-party", Value = "ignored", Host = "example.invalid", Path = "/" },
            ],
        };
        using var client = TraditionalJwClient.FromStoredSession("20260001", "secret-pass", document);

        var exported = client.ExportStoredSession();

        Assert.Equal(3, exported.Cookies.Count);
        Assert.Contains(exported.Cookies, item => item.Name == "route");
        Assert.DoesNotContain(exported.Cookies, item => item.Name == "third-party");
    }

    internal sealed class TraditionalJwFakeHandler : HttpMessageHandler
    {
        private static readonly byte[] CaptchaImage = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><text>2468</text></svg>");

        public bool OmitJwSessionCookie { get; init; }
        public bool LoginPostWithoutTicket { get; init; }
        public bool FailPreloginRequests { get; init; }
        public string DelayStage { get; init; } = string.Empty;
        public List<CapturedRequest> Requests { get; } = [];
        public int CaptchaRequests { get; private set; }
        public int LoginPosts { get; private set; }
        public int DirectLoginPosts { get; private set; }
        public int DirectFailuresBeforeSuccess { get; init; }
        public string DirectFailureMessage { get; init; } = "验证码错误!!";
        public int ScheduleLoadRequests { get; private set; }
        public int CourseRoundRequests { get; private set; }
        public int PreviewEntryRequests { get; private set; }
        public int PreviewDataRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(
                pair => pair.Key,
                pair => string.Join(",", pair.Value),
                StringComparer.OrdinalIgnoreCase);
            var contentHeaders = request.Content?.Headers.ToDictionary(
                pair => pair.Key,
                pair => string.Join(",", pair.Value),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body, headers, contentHeaders));
            var uri = request.RequestUri!;
            if (uri.Host == "222.243.161.213" && uri.AbsolutePath == "/")
            {
                return Text(HttpStatusCode.OK, "<html>JW login</html>");
            }

            if (uri.Host == "222.243.161.213" && uri.AbsolutePath == "/verifycode.servlet")
            {
                CaptchaRequests++;
                return Bytes(HttpStatusCode.OK, CaptchaImage, "image/jpeg");
            }

            if (uri.Host == "222.243.161.213" && uri.AbsolutePath == "/Logon.do" && uri.Query.Contains("flag=sess", StringComparison.OrdinalIgnoreCase))
            {
                return Text(HttpStatusCode.OK, "abcdefghijklmnopqrstuvwxyz#11111111111111111111");
            }

            if (uri.Host == "222.243.161.213" && uri.AbsolutePath == "/Logon.do")
            {
                DirectLoginPosts++;
                if (DirectLoginPosts <= DirectFailuresBeforeSuccess)
                    return Text(HttpStatusCode.OK, $"<font id='showMsg'>{DirectFailureMessage}</font>");
                var response = Text(HttpStatusCode.OK, "ok");
                response.Headers.Location = new Uri("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
                return response;
            }
            if (uri.Host == "sso.hnsoftedu.com" && uri.AbsolutePath == "/login" && request.Method == HttpMethod.Get)
            {
                await DelayIfNeededAsync(uri.Query.Contains("jw.hnsoftedu.com", StringComparison.OrdinalIgnoreCase) ? "jw_ticket" : "login_page", cancellationToken);
                var query = Uri.UnescapeDataString(uri.Query);
                if (query.Contains("my.hnsoftedu.com/cas/validate", StringComparison.Ordinal))
                {
                    return Text(HttpStatusCode.OK, """
                    <html>
                    <p id="login-page-flowkey">flow-1</p>
                    <p id="login-croypto">MTIzNDU2Nzg=</p>
                    <p id="captcha-url">api/captcha/generate/DEFAULT</p>
                    <p id="redirect-uri"></p>
                    </html>
                    """);
                }

                if (query.Contains("jw.hnsoftedu.com/sso.jsp", StringComparison.Ordinal))
                {
                    return Redirect("http://jw.hnsoftedu.com/sso.jsp?ticket=ST-JW");
                }
            }

            if (uri.Host == "sso.hnsoftedu.com" && uri.AbsolutePath == "/login" && request.Method == HttpMethod.Post)
            {
                await DelayIfNeededAsync("portal_login", cancellationToken);
                LoginPosts++;
                if (LoginPostWithoutTicket)
                {
                    return Text(HttpStatusCode.OK, "login failed");
                }

                var response = Redirect("http://my.hnsoftedu.com/cas/validate?ticket=ST-PORTAL");
                response.Headers.TryAddWithoutValidation("Set-Cookie", "SOURCEID_TGC=tgc-value; Domain=sso.hnsoftedu.com; Path=/");
                return response;
            }

            if (uri.Host == "sso.hnsoftedu.com" && uri.AbsolutePath.Contains("/api/captcha/", StringComparison.Ordinal))
            {
                await DelayIfNeededAsync("captcha_image", cancellationToken);
                CaptchaRequests++;
                return Bytes(HttpStatusCode.OK, CaptchaImage, "image/svg+xml");
            }

            if (uri.Host == "sso.hnsoftedu.com")
            {
                if (FailPreloginRequests)
                {
                    throw new HttpRequestException("prelogin offline");
                }

                return Text(HttpStatusCode.OK, "{}");
            }

            if (uri.Host == "my.hnsoftedu.com" && uri.AbsolutePath == "/cas/validate")
            {
                return Text(HttpStatusCode.OK, "ok");
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/sso.jsp" && string.IsNullOrWhiteSpace(uri.Query))
            {
                await DelayIfNeededAsync("jw_sso_entry", cancellationToken);
                return Text(HttpStatusCode.OK, "jw-sso-entry");
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/sso.jsp" && uri.Query.Contains("ticket=", StringComparison.Ordinal))
            {
                await DelayIfNeededAsync("jw_redirect", cancellationToken);
                var response = Redirect("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
                if (!OmitJwSessionCookie)
                {
                    response.Headers.TryAddWithoutValidation("Set-Cookie", "JSESSIONID=jw-session; Domain=jw.hnsoftedu.com; Path=/jsxsd");
                }

                return response;
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath.EndsWith("/xsMainV.jsp", StringComparison.Ordinal))
            {
                await DelayIfNeededAsync("jw_main", cancellationToken);
                return Text(HttpStatusCode.OK, "main");
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/jsxsd/grxx/xsxx")
            {
                return Text(HttpStatusCode.OK, """
                <table>
                  <tr><th>学号</th><td>20260001</td><th>姓名</th><td>测试同学</td></tr>
                  <tr><th>学院</th><td>信息工程学院</td><th>专业</th><td>软件技术</td></tr>
                  <tr><th>班级</th><td>软件一班</td><th>培养层次</th><td>专科</td></tr>
                </table>
                """);
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath.EndsWith("/xsMainV_new.jsp", StringComparison.Ordinal))
            {
                await DelayIfNeededAsync("jw_schedule_params", cancellationToken);
                return Text(HttpStatusCode.OK, """
                <html>
                <ul><li name="kbjcmsid" data-value="1" class="layui-this">按节次</li></ul>
                <select id="week">
                <option value="2026-04-20" selected>第8周</option>
                <option value="2026-04-27">第9周</option>
                </select>
                </html>
                """);
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath.EndsWith("/mainV_index_loadkb.jsp", StringComparison.Ordinal))
            {
                await DelayIfNeededAsync("jw_schedule_html", cancellationToken);
                ScheduleLoadRequests++;
                return Text(HttpStatusCode.OK, """
                <table>
                <tr>
                <td>上午<br/>第1-2节<br/>08:00-09:35</td>
                <td>高等数学<br><font title="教师">张老师</font><font title="教室">1-101</font><font title="周次(节次)">8(周)[01-02节]</font></td>
                <td></td><td></td><td></td><td></td><td></td><td></td>
                </tr>
                </table>
                """);
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/jsxsd/xsxk/xklc_list")
            {
                CourseRoundRequests++;
                return Text(HttpStatusCode.OK, """
                <html><body>
                <table><tbody>
                <tr>
                  <td>2025-2026-2</td>
                  <td>公共选修课第一轮</td>
                  <td>2026-04-23 ~ 2026-04-25</td>
                  <td>开放</td>
                  <td><a onclick="jrxk('0','round-1')">预览</a></td>
                </tr>
                </tbody></table>
                </body></html>
                """);
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/jsxsd/xsxk/yxxsxk_index")
            {
                PreviewEntryRequests++;
                return Text(HttpStatusCode.OK, "preview entry");
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/jsxsd/xsxkkc/yl_comeInGgxxkxk")
            {
                return Text(HttpStatusCode.OK, "preview come in");
            }

            if (uri.Host == "jw.hnsoftedu.com" && uri.AbsolutePath == "/jsxsd/xsxkkc/yl_xsxkGgxxkxk")
            {
                PreviewDataRequests++;
                return Json(HttpStatusCode.OK, """
                {
                  "aaData": [
                    [
                      "A1001",
                      "大学美育",
                      "2",
                      "李老师",
                      "周一 1-2 节",
                      "教学楼 101",
                      "主校区",
                      "30",
                      "12",
                      "",
                      "公共选修",
                      "<button onclick=\"chooseCourse('course-1001','section-2002')\">选择</button>"
                    ]
                  ]
                }
                """);
            }

            return Text(HttpStatusCode.NotFound, $"not found: {uri}");
        }

        private async Task DelayIfNeededAsync(string stage, CancellationToken cancellationToken)
        {
            if (string.Equals(DelayStage, stage, StringComparison.Ordinal))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        private static HttpResponseMessage Text(HttpStatusCode statusCode, string text)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(text, Encoding.UTF8, "text/html"),
            };
        }

        private static HttpResponseMessage Bytes(HttpStatusCode statusCode, byte[] bytes, string contentType)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(bytes)
                {
                    Headers =
                    {
                        ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType),
                    },
                },
            };
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string text)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(text, Encoding.UTF8, "application/json"),
            };
        }

        private static HttpResponseMessage Redirect(string location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(location);
            return response;
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Mobile HTTP should not be used by traditional JW tests.");
        }
    }

    internal sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string Body,
        Dictionary<string, string> Headers,
        Dictionary<string, string> ContentHeaders);
}
