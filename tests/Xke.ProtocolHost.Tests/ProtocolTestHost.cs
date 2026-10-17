using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.ProtocolHost;

namespace Xke.ProtocolHost.Tests;

internal sealed class ProtocolTestHost : IAsyncDisposable
{
    public const string ApiKey = "fake-protocol-api-key";
    private readonly WebApplication _app;
    public HttpClient Client { get; }
    public string Root { get; }
    public SchoolHandler School { get; }
    public CaptchaRecognizer Captcha { get; }

    private ProtocolTestHost(WebApplication app, string root, SchoolHandler school, CaptchaRecognizer captcha)
    {
        _app = app;
        Root = root;
        School = school;
        Captcha = captcha;
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Client = new HttpClient { BaseAddress = new Uri(address) };
        Client.DefaultRequestHeaders.Add(XkeProtocolHostApplication.ApiKeyHeader, ApiKey);
    }

    public static async Task<ProtocolTestHost> StartAsync(string? existingRoot = null)
    {
        var root = existingRoot ?? Path.Combine(Path.GetTempPath(), "xke-protocol-tests", Guid.NewGuid().ToString("N"));
        var school = new SchoolHandler();
        var captcha = new CaptchaRecognizer();
        var app = XkeProtocolHostApplication.Build(new ProtocolHostOptions
        {
            ApiKey = ApiKey, DataRootPath = root,
            DdddOcrEndpoint = new Uri("http://127.0.0.1:8866/recognize"), Urls = ["http://127.0.0.1:0"],
        }, services =>
        {
            services.AddLogging(logging => logging.ClearProviders());
            services.AddSingleton<ICaptchaRecognizer>(captcha);
            services.AddSingleton<Func<string, string, DirectJwLoginProtocol>>(_ => (user, password)
                => new DirectJwLoginProtocol(user, password, new HttpClient(school, disposeHandler: false)));
        });
        await app.StartAsync();
        return new ProtocolTestHost(app, root, school, captcha);
    }

    public Task<HttpResponseMessage> LoginAsync(string user = "test-login-account", string password = "fake-school-password")
        => Client.PostAsJsonAsync("/api/auth/login", new { user, password });

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        School.Dispose();
        // Remove only this generated test directory, after verifying its root.
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xke-protocol-tests"));
        if (Directory.Exists(Root) && Path.GetDirectoryName(Path.GetFullPath(Root)) == parent)
            Directory.Delete(Root, recursive: true);
    }
}

internal sealed class CaptchaRecognizer : ICaptchaRecognizer
{
    public int Calls { get; private set; }
    public JwScheduleException? Failure { get; set; }
    public Task<string> RecognizeAsync(string captchaImageDataUrl, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Failure is not null) throw Failure;
        if (!captchaImageDataUrl.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal))
            throw new InvalidOperationException("Expected a synthetic image data URL.");
        return Task.FromResult("abcd");
    }
}

internal sealed class SchoolHandler : HttpMessageHandler
{
    public bool Expired { get; set; }
    public bool Http401OnExpiration { get; set; }
    public bool EmptyWeek { get; set; }
    public bool RejectLogin { get; set; }
    public bool FailSchedule { get; set; }
    public int LoginSubmissions { get; private set; }
    public TimeSpan ProtectedDelay { get; set; }
    public int MaxConcurrentProtectedRequests { get; private set; }
    private int _activeProtectedRequests;
    private readonly object _requestGate = new();
    public List<(HttpMethod Method, string Path)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isProtected = request.RequestUri!.AbsolutePath.StartsWith("/jsxsd/", StringComparison.Ordinal);
        if (isProtected)
        {
            lock (_requestGate)
                MaxConcurrentProtectedRequests = Math.Max(MaxConcurrentProtectedRequests, ++_activeProtectedRequests);
        }
        try
        {
            if (isProtected && ProtectedDelay > TimeSpan.Zero)
                await Task.Delay(ProtectedDelay, cancellationToken);
            return await RespondAsync(request, cancellationToken);
        }
        finally
        {
            if (isProtected)
                lock (_requestGate) _activeProtectedRequests--;
        }
    }

    private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requests.Add((request.Method, uri.AbsolutePath));
        if (uri.AbsolutePath == "/") return Html("<html>School login</html>", request);
        if (uri.AbsolutePath == "/verifycode.servlet")
        {
            var response = Html("", request);
            response.Content = new ByteArrayContent([1, 2, 3]);
            response.Content.Headers.ContentType = new("image/jpeg");
            return response;
        }
        if (uri.AbsolutePath == "/Logon.do")
        {
            if (uri.Query.Contains("flag=sess", StringComparison.Ordinal)) return Html("test-padding#00000000000000000000", request);
            LoginSubmissions++;
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!form.Contains("RANDOMCODE=abcd", StringComparison.Ordinal)) throw new InvalidOperationException("OCR code was not submitted.");
            var response = Html("", request);
            if (!RejectLogin)
                response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
            return response;
        }
        if (Expired)
        {
            var response = Html("{\"flag1\":2,\"msgContent\":\"need login\"}", request);
            if (Http401OnExpiration) response.StatusCode = HttpStatusCode.Unauthorized;
            return response;
        }
        if (uri.AbsolutePath == "/jsxsd/grxx/xsxx")
        {
            var response = Html(Profile, request);
            response.Headers.TryAddWithoutValidation("Set-Cookie", "JSESSIONID=fake-school-cookie; Path=/jsxsd");
            return response;
        }
        if (uri.AbsolutePath.EndsWith("xsMainV.jsp", StringComparison.Ordinal)) return Html("<html>Workspace</html>", request);
        if (uri.AbsolutePath.EndsWith("xsMainV_new.jsp", StringComparison.Ordinal)) return Html(Home, request);
        if (FailSchedule) return new(HttpStatusCode.ServiceUnavailable) { RequestMessage = request, Content = new StringContent("test unavailable") };
        if (uri.AbsolutePath.EndsWith("mainV_index_loadkb.jsp", StringComparison.Ordinal)) return Html(Grid(EmptyWeek), request);
        if (uri.AbsolutePath == "/jsxsd/xskb/xskb_list.do")
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            return Html(Semester(query.GetValueOrDefault("xnxq01id").ToString(), query.GetValueOrDefault("zc").ToString()), request);
        }
        throw new InvalidOperationException($"Unexpected fake school route {uri.AbsolutePath}");
    }

    private static HttpResponseMessage Html(string html, HttpRequestMessage request)
        => new(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private const string Profile = """
        <table id="xjkpTable"><tr><td colspan="8">学籍卡片</td></tr>
        <tr><td>学号：SERVER001</td><td>院系：Test Academy</td><td>专业：Test Major</td><td>班级：Test Class</td><td>学制：4</td></tr>
        <tr><td>姓名</td><td>Test Student</td><td>学习层次</td><td>职业本科</td></tr>
        <tr><td>入学日期</td><td>20240901</td></tr></table>
        """;
    private const string Home = """
        <li name="kbjcmsid" data-value="mode-test" class="layui-this">默认模式</li>
        <select id="term"><option selected>2026-2027-1</option></select>
        <select id="week"><option value="2026-09-07" selected>第一周</option><option value="2026-09-14">第二周</option></select>
        """;

    private static string Course => """
        Test Course<br><font title="教师">Test Teacher</font><br>
        <font title="周次(节次)">1-16(周)[01-02-03-04节]</font><br>
        <font title="教室">Test Room</font><font name="xsks">(理论:32)</font>
        """;

    private static string Grid(bool empty) => $"""
        <table id="timetable"><tr><td></td><td>星期一</td><td>星期二</td></tr>
        <tr><td>第一大节(01,02小节)08:00-09:40</td><td><div id="row-1-2" class="kbcontent">{(empty ? "" : Course)}</div></td><td></td></tr>
        <tr><td>第二大节(03,04小节)10:00-11:40</td><td><div id="row2-1-2" class="kbcontent">{(empty ? "" : Course)}</div></td><td></td></tr>
        <tr><td>备注</td><td>Test timetable note</td></tr></table>
        """;

    private string Semester(string term, string week)
    {
        term = term.Length == 0 ? "2026-2027-1" : term;
        return $"""
            <select id="xnxq01id"><option value="2026-2027-1" {(term == "2026-2027-1" ? "selected" : "")}>2026-2027-1</option><option value="2025-2026-2" {(term == "2025-2026-2" ? "selected" : "")}>2025-2026-2</option></select>
            <select id="kbjcmsid"><option selected value="mode-test">默认模式</option></select>
            <select id="zc"><option value="" {(week.Length == 0 ? "selected" : "")}>全部周</option><option value="1" {(week == "1" ? "selected" : "")}>第一周</option><option value="2" {(week == "2" ? "selected" : "")}>第二周</option></select>
            {Grid(EmptyWeek)}
            """;
    }
}
