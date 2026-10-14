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
        IJwAuthClient? jwAuthClient = null,
        IJwScheduleClient? jwScheduleClient = null,
        HttpMessageHandler? networkHttpMessageHandler = null,
        INetworkCommandExecutor? networkCommandExecutor = null,
        IDesktopWindowController? desktopWindowController = null,
        IDesktopStartupRegistrar? desktopStartupRegistrar = null)
    {
        return await StartAsync(
            webRoot.RootPath,
            dataRoot,
            hostMode,
            jwAuthClient,
            jwScheduleClient,
            networkHttpMessageHandler,
            networkCommandExecutor,
            desktopWindowController,
            desktopStartupRegistrar);
    }

    public static async Task<StartedWebHost> StartAsync(
        string webRootPath,
        TemporaryDirectory? dataRoot = null,
        string hostMode = "test-host",
        IJwAuthClient? jwAuthClient = null,
        IJwScheduleClient? jwScheduleClient = null,
        HttpMessageHandler? networkHttpMessageHandler = null,
        INetworkCommandExecutor? networkCommandExecutor = null,
        IDesktopWindowController? desktopWindowController = null,
        IDesktopStartupRegistrar? desktopStartupRegistrar = null)
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
            JwAuthClient = jwAuthClient,
            UseLegacyMobileAuthentication = true,
            UseLegacyMobileScheduleForTests = true,
            JwScheduleClient = jwScheduleClient,
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
