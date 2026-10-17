using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Desktop;
using Xke.Core.Network;
using Xke.Core.Schedule;
using Xke.WebHost;

var repo = new DirectoryInfo(AppContext.BaseDirectory);
while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "frontend", "package.json"))) repo = repo.Parent;
var repoRoot = repo?.FullName ?? Directory.GetCurrentDirectory();
var webRoot = Path.GetFullPath(args.Length > 0 ? args[0] : "frontend/dist", repoRoot);
if (!File.Exists(Path.Combine(webRoot, "index.html")))
    throw new InvalidOperationException("Build frontend/dist first, or pass the built web root as the first argument.");
var dataRoot = Path.Combine(Path.GetTempPath(), $"xke-frontend-integration-{Guid.NewGuid():N}");
Directory.CreateDirectory(dataRoot);
var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
{
    Urls = ["http://127.0.0.1:0"],
    WebRootPath = webRoot,
    AuthDataRootPath = dataRoot,
    RuntimeChannel = "isolated-frontend-integration",
    HostMode = "desktop-webview",
    DesktopRequestToken = "synthetic-integration-desktop-proof",
    EnableBackgroundTasks = false,
    ScheduleApiOptions = new ScheduleApiOptions(),
    JwScheduleClient = new FixtureScheduleClient(),
    JwLoginProtocolFactory = (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(new FixtureProtocolHandler(user))),
    CampusSelfServiceOptions = new CampusSelfServiceOptions(),
    NetworkHttpMessageHandler = new OfflineNetworkHandler(),
    NetworkCommandExecutor = new OfflineCommands(),
    DesktopStartupRegistrar = new InMemoryStartupRegistrar(),
});
var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
app.MapPost("/__integration/stop", () =>
{
    _ = Task.Run(async () => { await Task.Delay(250); stop.TrySetResult(); });
    return Results.Accepted();
});
try
{
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    Console.WriteLine($"INTEGRATION_URL={address}");
    Console.WriteLine($"ISOLATED_DATA_ROOT={dataRoot}");
    Console.WriteLine("Synthetic account: 20260001; password: test-password; captcha: 1234. No external requests or system changes.");
    Console.WriteLine("First initialization succeeds; first refresh needs captcha (wrong codes are retryable); second refresh fails; subsequent refreshes succeed.");
    await Task.WhenAny(stop.Task, Task.Delay(TimeSpan.FromMinutes(30)));
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
    Directory.Delete(dataRoot, recursive: true);
}

internal static class FixtureCaptcha
{
    public static byte[] Image { get; } = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"160\" height=\"48\"><rect width=\"160\" height=\"48\" fill=\"#eef2f8\"/><text x=\"20\" y=\"34\" font-size=\"30\" fill=\"#172b4d\">1234</text></svg>");
    public static string DataUrl => "data:image/svg+xml;base64," + Convert.ToBase64String(Image);
}

internal sealed class FixtureProtocolHandler(string user) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (uri.AbsolutePath == "/verifycode.servlet")
        {
            var content = new ByteArrayContent(FixtureCaptcha.Image);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/svg+xml");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
        }
        if (uri.Query.Contains("flag=sess", StringComparison.Ordinal))
            return Response(request, "abcdefghijklmnopqrstuvwxyz#11111111111111111111");
        if (uri.AbsolutePath == "/Logon.do")
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = Response(request, "<html>Fixture login</html>");
            if (form.Split('&').Contains("RANDOMCODE=1234", StringComparer.Ordinal))
                response.Headers.Location = new Uri("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
            return response;
        }
        return Response(request, $"<table><tr><th>学号</th><td>{WebUtility.HtmlEncode(user)}</td><th>姓名</th><td>联调测试同学</td></tr><tr><th>学院</th><td>测试学院</td><th>专业</th><td>软件</td></tr></table>");
    }

    private static HttpResponseMessage Response(HttpRequestMessage request, string text)
        => new(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(text) };
}

internal sealed class FixtureScheduleClient : IJwScheduleClient
{
    private int _cookieCalls;

    public Task<JwScheduleFetchResult> FetchSemesterAsync(string user, string password, string preloginMode, string academicTerm,
        string sessionId, CancellationToken cancellationToken = default)
    {
        var term = string.IsNullOrWhiteSpace(academicTerm) ? "2026-2027-1" : academicTerm;
        if (term is not ("2026-2027-1" or "2025-2026-2"))
            throw new JwScheduleException("隔离联调：学期不存在", 404);
        var schedule = BuildSchedule(new ScheduleRequest());
        return Task.FromResult(schedule with
        {
            WeekLabel = "全部周", TermLabel = term,
            RawPayload = new Dictionary<string, object?>
            {
                ["term"] = new Dictionary<string, object?> { ["academic_term"] = term, ["label"] = term },
                ["available_terms"] = new List<Dictionary<string, object?>>
                {
                    new() { ["value"] = "2026-2027-1", ["label"] = "2026-2027-1", ["selected"] = term == "2026-2027-1" },
                    new() { ["value"] = "2025-2026-2", ["label"] = "2025-2026-2", ["selected"] = term == "2025-2026-2" },
                },
                ["notes"] = new[] { "合成学期数据，仅供隔离联调" },
                ["supplementary_courses"] = Array.Empty<Dictionary<string, string>>(),
            },
        });
    }

    public Task<JwScheduleFetchResult> FetchCookieAsync(string user, string password, string preloginMode, ScheduleRequest request,
        CancellationToken cancellationToken = default, string sessionId = "")
    {
        var call = Interlocked.Increment(ref _cookieCalls);
        if (call == 2)
            throw new JwCaptchaRequiredException("隔离联调：输入验证码刷新课表", FixtureCaptcha.DataUrl, 120,
                (code, token) => code == "1234"
                    ? Task.FromResult(BuildSchedule(request))
                    : Task.FromException<JwScheduleFetchResult>(new JwScheduleException("隔离联调：验证码错误，可重试", 400)));
        if (call == 3)
            throw new JwScheduleException("隔离联调：刷新失败，已有课表应保留", 503);
        return Task.FromResult(BuildSchedule(request));
    }

    private static JwScheduleFetchResult BuildSchedule(ScheduleRequest request)
    {
        var week = ScheduleService.ParseWeekNumber(request.ManualWeek) ?? 8;
        var today = DateTime.Today;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays((week - 8) * 7);
        string[] days = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
        var dates = days.Select((day, index) => (day, date: monday.AddDays(index).ToString("yyyy-MM-dd")))
            .ToDictionary(item => item.day, item => item.date);
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["course_name"] = "联调高等数学", ["weekday"] = "星期一", ["section"] = "第1-2节", ["time_range"] = "08:10-09:45", ["teacher"] = "测试老师", ["room"] = "测试楼101", ["weeks"] = "1-16周", ["credit"] = "4" },
            new() { ["course_name"] = "联调软件工程", ["weekday"] = days[((int)today.DayOfWeek + 6) % 7], ["section"] = "第2-4节", ["time_range"] = "10:10-12:00", ["teacher"] = "测试老师", ["room"] = "测试楼202", ["weeks"] = "1-16周", ["credit"] = "3.5" },
            new() { ["course_name"] = "联调时间待定课程", ["weekday"] = "待定", ["section"] = "待定", ["time_range"] = "", ["teacher"] = "测试老师", ["room"] = "待定", ["weeks"] = $"第{week}周", ["credit"] = "" },
        };
        foreach (var row in rows)
        {
            row["section_numbers"] = row["section"]?.ToString() switch
            {
                "第1-2节" => new[] { 1, 2 }, "第2-4节" => new[] { 2, 3, 4 }, _ => Array.Empty<int>(),
            };
            row["week_numbers"] = row["weeks"]?.ToString() == "1-16周" ? Enumerable.Range(1, 16).ToArray() : new[] { week };
            row["time_segments"] = string.IsNullOrWhiteSpace(row["time_range"]?.ToString()) ? Array.Empty<string>() : new[] { row["time_range"]!.ToString()! };
        }
        return new JwScheduleFetchResult("jw_schedule", "教务系统课表", "jw", $"第{week}周", 8, week,
            [new() { ["value"] = "8", ["label"] = "第8周", ["selected"] = week == 8 }, new() { ["value"] = "9", ["label"] = "第9周", ["selected"] = week == 9 }],
            dates,
            [new() { ["section"] = "第1-2节", ["block_time"] = "08:10-09:45" }, new() { ["section"] = "第2-4节", ["block_time"] = "10:10-12:00" }],
            [new() { ["message"] = "隔离联调数据，不是用户真实课表" }], rows,
            new() { ["retrieval_channel"] = "direct_jw", ["updated_at"] = DateTimeOffset.Now.ToString("O"), ["term"] = new Dictionary<string, object?> { ["academic_term"] = "2026-2027-1", ["term_label"] = "联调秋季学期" } },
            "隔离联调课表", "");
    }
}

internal sealed class OfflineNetworkHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request, Content = new StringContent("isolated-offline") });
}

internal sealed class OfflineCommands : INetworkCommandExecutor
{
    public Task<NetworkCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.FromResult(new NetworkCommandResult(1, string.Empty, "isolated-fixture-no-system-commands"));
}

internal sealed class InMemoryStartupRegistrar : IDesktopStartupRegistrar
{
    private bool _enabled;
    public bool? GetRegistrationState() => _enabled;
    public void SetRegistrationState(bool enabled) => _enabled = enabled;
}
