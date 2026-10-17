using System.Net;
using System.Text.Json;
using Xke.Core.Auth.Jw;
using Xke.Core.Desktop;
using Xke.Core.Network;
using Xke.Core.Schedule;
using Xke.WebHost;

namespace Xke.WebHost.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(string prefix = "xke-test-data")
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

internal sealed class TemporaryWebRoot : IDisposable
{
    public TemporaryWebRoot(string prefix = "xke-webhost-test")
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(Path.Combine(RootPath, "assets"));
        Directory.CreateDirectory(Path.Combine(RootPath, "assets", "modules"));

        File.WriteAllText(Path.Combine(RootPath, "index.html"), "<html><body>XKE Test Shell</body></html>");
        File.WriteAllText(Path.Combine(RootPath, "favicon.ico"), "test-icon");
        File.WriteAllText(Path.Combine(RootPath, "shell-static"), "test-shell-static");
        File.WriteAllText(
            Path.Combine(RootPath, "assets", "app.js"),
            "const FRONTEND_BUILD = \"test-shell-build\"; console.log('shell-ready');");
        File.WriteAllText(Path.Combine(RootPath, "assets", "modules", "profile.js"), "export const profile = true;");
    }

    public string RootPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

internal sealed class StartedWebHost : IAsyncDisposable
{
    private readonly TemporaryDirectory? _ownedDataRoot;

    public StartedWebHost(XkeWebHostService service, XkeWebHostLaunchResult launchResult, TemporaryDirectory? ownedDataRoot = null)
    {
        _ownedDataRoot = ownedDataRoot;
        Service = service;
        LaunchResult = launchResult;
        Client = new HttpClient { BaseAddress = new Uri(launchResult.BaseUrl) };
        if (service.DesktopRequestToken is { } token) Client.DefaultRequestHeaders.Add(LocalApiAccessPolicy.DesktopTokenHeaderName, token);
    }

    public XkeWebHostService Service { get; }
    public XkeWebHostLaunchResult LaunchResult { get; }
    public HttpClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Service.DisposeAsync();
        _ownedDataRoot?.Dispose();
    }
}

internal static class WebHostTestServer
{
    public static async Task<StartedWebHost> StartAsync(
        TemporaryWebRoot webRoot,
        TemporaryDirectory? dataRoot = null,
        string hostMode = "test-host",
        IJwScheduleClient? jwScheduleClient = null,
        HttpMessageHandler? networkHttpMessageHandler = null,
        INetworkCommandExecutor? networkCommandExecutor = null,
        IDesktopWindowController? desktopWindowController = null,
        IDesktopStartupRegistrar? desktopStartupRegistrar = null,
        Func<string, string, DirectJwLoginProtocol>? jwLoginProtocolFactory = null)
    {
        return await StartAsync(
            webRoot.RootPath,
            dataRoot,
            hostMode,
            jwScheduleClient,
            networkHttpMessageHandler,
            networkCommandExecutor,
            desktopWindowController,
            desktopStartupRegistrar,
            jwLoginProtocolFactory);
    }

    public static async Task<StartedWebHost> StartAsync(
        string webRootPath,
        TemporaryDirectory? dataRoot = null,
        string hostMode = "test-host",
        IJwScheduleClient? jwScheduleClient = null,
        HttpMessageHandler? networkHttpMessageHandler = null,
        INetworkCommandExecutor? networkCommandExecutor = null,
        IDesktopWindowController? desktopWindowController = null,
        IDesktopStartupRegistrar? desktopStartupRegistrar = null,
        Func<string, string, DirectJwLoginProtocol>? jwLoginProtocolFactory = null)
    {
        var ownedDataRoot = dataRoot is null ? new TemporaryDirectory() : null;
        var service = new XkeWebHostService(new XkeWebHostOptions
        {
            HostMode = hostMode,
            DesktopRequestToken = hostMode == "desktop-webview" ? "synthetic-desktop-capability" : null,
            RuntimeChannel = "dotnet8-test",
            WebRootPath = webRootPath,
            EnableBackgroundTasks = false,
            ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(),
            AuthDataRootPath = (dataRoot ?? ownedDataRoot!).RootPath,
            JwScheduleClient = jwScheduleClient,
            // Tests that do not assert on school-protocol traffic get a working fake login.
            JwLoginProtocolFactory = jwLoginProtocolFactory ?? ((user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(new ProtocolHandler()))),
            NetworkHttpMessageHandler = networkHttpMessageHandler,
            NetworkCommandExecutor = networkCommandExecutor,
            DesktopWindowController = desktopWindowController,
            DesktopStartupRegistrar = desktopStartupRegistrar,
            Urls = new[] { "http://127.0.0.1:0" },
        });
        try { return new StartedWebHost(service, await service.StartAsync(), ownedDataRoot); }
        catch
        {
            await service.DisposeAsync();
            ownedDataRoot?.Dispose();
            throw;
        }
    }
}

internal static class WebHostTestJson
{
    public static JsonElement GetData(JsonDocument document)
    {
        return document.RootElement.GetProperty("data");
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }
}

internal sealed class ProtocolHandler : HttpMessageHandler
{
    public int Requests { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        var uri = request.RequestUri!;
        if (uri.AbsolutePath == "/verifycode.servlet")
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        }
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(uri.Query.Contains("flag=sess", StringComparison.Ordinal)
                ? "abcdefghijklmnopqrstuvwxyz#11111111111111111111"
                : "<table><tr><th>学号</th><td>20260001</td><th>姓名</th><td>测试同学</td></tr><tr><th>学院</th><td>信息工程学院</td><th>专业</th><td>软件技术</td></tr><tr><th>班级</th><td>软件一班</td><th>培养层次</th><td>专科</td><th>校区</th><td>主校区</td></tr></table>"),
        };
        if (uri.AbsolutePath == "/Logon.do" && !uri.Query.Contains("flag=sess", StringComparison.Ordinal))
            response.Headers.Location = new Uri("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
        return Task.FromResult(response);
    }
}
