using System.Text.Json;
using Xke.Core.Auth.Jw;
using Xke.Core.Course;
using Xke.Core.Desktop;
using Xke.Core.Network;
using Xke.Core.Notice;
using Xke.Core.Schedule;
using Xke.Core.Telemetry;
using Xke.Core.Update;
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
    public StartedWebHost(XkeWebHostService service, XkeWebHostLaunchResult launchResult)
    {
        Service = service;
        LaunchResult = launchResult;
        Client = new HttpClient { BaseAddress = new Uri(launchResult.BaseUrl) };
    }

    public XkeWebHostService Service { get; }
    public XkeWebHostLaunchResult LaunchResult { get; }
    public HttpClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Service.DisposeAsync();
    }
}

internal static class WebHostTestServer
{
    public static async Task<StartedWebHost> StartAsync(
        TemporaryWebRoot webRoot,
        TemporaryDirectory? dataRoot = null,
        string hostMode = "test-host",
        IJwAuthClient? jwAuthClient = null,
        IJwScheduleClient? jwScheduleClient = null,
        ICourseClient? courseClient = null,
        INoticeRemoteSource? noticeRemoteSource = null,
        string noticeSigningPublicKeyPem = "",
        HttpMessageHandler? networkHttpMessageHandler = null,
        INetworkCommandExecutor? networkCommandExecutor = null,
        ITelemetryTransport? telemetryTransport = null,
        IDesktopWindowController? desktopWindowController = null,
        IDesktopStartupRegistrar? desktopStartupRegistrar = null,
        IUpdatePackageDownloader? updatePackageDownloader = null,
        IUpdateVersionProvider? updateVersionProvider = null,
        string? mainExecutablePath = null)
    {
        return await StartAsync(
            webRoot.RootPath,
            dataRoot,
            hostMode,
            jwAuthClient,
            jwScheduleClient,
            courseClient,
            noticeRemoteSource,
            noticeSigningPublicKeyPem,
            networkHttpMessageHandler,
            networkCommandExecutor,
            telemetryTransport,
            desktopWindowController,
            desktopStartupRegistrar,
            updatePackageDownloader,
            updateVersionProvider,
            mainExecutablePath);
    }

    public static async Task<StartedWebHost> StartAsync(
        string webRootPath,
        TemporaryDirectory? dataRoot = null,
        string hostMode = "test-host",
        IJwAuthClient? jwAuthClient = null,
        IJwScheduleClient? jwScheduleClient = null,
        ICourseClient? courseClient = null,
        INoticeRemoteSource? noticeRemoteSource = null,
        string noticeSigningPublicKeyPem = "",
        HttpMessageHandler? networkHttpMessageHandler = null,
        INetworkCommandExecutor? networkCommandExecutor = null,
        ITelemetryTransport? telemetryTransport = null,
        IDesktopWindowController? desktopWindowController = null,
        IDesktopStartupRegistrar? desktopStartupRegistrar = null,
        IUpdatePackageDownloader? updatePackageDownloader = null,
        IUpdateVersionProvider? updateVersionProvider = null,
        string? mainExecutablePath = null)
    {
        var service = new XkeWebHostService(new XkeWebHostOptions
        {
            HostMode = hostMode,
            RuntimeChannel = "dotnet8-test",
            WebRootPath = webRootPath,
            AuthDataRootPath = dataRoot?.RootPath ?? string.Empty,
            JwAuthClient = jwAuthClient,
            JwScheduleClient = jwScheduleClient,
            CourseClient = courseClient,
            NoticeRemoteSource = noticeRemoteSource ?? new MissingNoticeRemoteSource(),
            NoticeSigningPublicKeyPem = noticeSigningPublicKeyPem,
            TelemetryTransport = telemetryTransport ?? new LocalOnlyTelemetryTransport(),
            NetworkHttpMessageHandler = networkHttpMessageHandler,
            NetworkCommandExecutor = networkCommandExecutor,
            DesktopWindowController = desktopWindowController,
            DesktopStartupRegistrar = desktopStartupRegistrar,
            UpdatePackageDownloader = updatePackageDownloader,
            UpdateVersionProvider = updateVersionProvider,
            MainExecutablePath = mainExecutablePath,
            Urls = new[] { "http://127.0.0.1:0" },
        });
        return new StartedWebHost(service, await service.StartAsync());
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
