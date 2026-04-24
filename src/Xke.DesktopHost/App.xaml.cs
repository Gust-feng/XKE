using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Core.Runtime;
using Xke.Storage;
using Xke.WebHost;

namespace Xke.DesktopHost;

public partial class App : System.Windows.Application
{
    private XkeWebHostService? _webHostService;
    private XkeDataRoot? _dataRoot;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        var resolvedPaths = XkePathResolver.Resolve();
        var dotNetRoot = ResolveDotNetWorkspaceRoot(resolvedPaths.AppBaseDirectory);
        _dataRoot = XkeDataRootFactory.CreateDefault(dotNetRoot, dotNetRoot);

        if (await TryActivateRunningInstanceAsync(_dataRoot).ConfigureAwait(true))
        {
            Shutdown();
            return;
        }

        MainWindow? window = null;
        _webHostService = new XkeWebHostService(new XkeWebHostOptions
        {
            HostMode = "desktop-webview",
            RuntimeChannel = "dotnet8-desktop",
            Urls = new[] { "http://127.0.0.1:0" },
            DesktopWindowController = new DeferredDesktopWindowController(() => window),
        });

        window = new MainWindow(_webHostService, _dataRoot);
        MainWindow = window;
        window.Show();
        await window.InitializeAsync().ConfigureAwait(true);

        if (_webHostService.LaunchResult?.BaseUrl is { } baseUrl)
        {
            _dataRoot.DesktopRuntime().Save(ResolvePort(baseUrl), Environment.ProcessId);
        }
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        _dataRoot?.DesktopRuntime().Clear(Environment.ProcessId);
        if (_webHostService is not null)
        {
            await _webHostService.DisposeAsync();
        }

        base.OnExit(e);
    }

    private static async Task<bool> TryActivateRunningInstanceAsync(XkeDataRoot dataRoot)
    {
        var instance = dataRoot.DesktopRuntime().Load();
        if (!instance.HasEndpoint)
        {
            return false;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{instance.Port}"),
            Timeout = TimeSpan.FromSeconds(2),
        };

        try
        {
            var health = await client.GetAsync("/api/health").ConfigureAwait(true);
            if (!health.IsSuccessStatusCode)
            {
                dataRoot.DesktopRuntime().Clear();
                return false;
            }

            var activate = await client.PostAsJsonAsync("/api/desktop/activate", new { }).ConfigureAwait(true);
            if (!activate.IsSuccessStatusCode)
            {
                dataRoot.DesktopRuntime().Clear();
                return false;
            }

            // Only treat the existing process as authoritative when it can surface a real desktop window.
            var payload = await activate.Content.ReadFromJsonAsync<ApiEnvelope>().ConfigureAwait(true);
            if (payload?.Data is JsonElement dataElement
                && dataElement.TryGetProperty("activated", out var activatedElement)
                && activatedElement.GetBoolean())
            {
                return true;
            }

            dataRoot.DesktopRuntime().Clear();
        }
        catch
        {
            dataRoot.DesktopRuntime().Clear();
        }

        return false;
    }

    private static int ResolvePort(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Port : 0;
    }

    private static string ResolveDotNetWorkspaceRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XKE.DotNet8.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }

    private sealed class DeferredDesktopWindowController : Xke.Core.Desktop.IDesktopWindowController
    {
        private readonly Func<MainWindow?> _windowAccessor;

        public DeferredDesktopWindowController(Func<MainWindow?> windowAccessor)
        {
            _windowAccessor = windowAccessor;
        }

        public Xke.Contracts.Responses.DesktopActivatePayload Activate()
        {
            var window = _windowAccessor();
            return window is null
                ? new Xke.Core.Desktop.NoopDesktopWindowController().Activate()
                : window.ActivateDesktopWindow();
        }
    }
}
