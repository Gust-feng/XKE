using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Api;
using Xke.Core.Runtime;
using Xke.Core.Releases;
using Xke.DesktopHost.Updates;
using Xke.Storage;
using Xke.WebHost;

namespace Xke.DesktopHost;

public partial class App : System.Windows.Application
{
    private XkeWebHostService? _webHostService;
    private XkeDataRoot? _dataRoot;
    private DesktopSingleInstance? _singleInstance;
    private AppUpdateService? _appUpdates;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        var resolvedPaths = XkePathResolver.Resolve();
        var dotNetRoot = ResolveDotNetWorkspaceRoot(resolvedPaths.AppBaseDirectory);
        _dataRoot = XkeDataRootFactory.CreateDefault(dotNetRoot, dotNetRoot);
        var startupRegistrar = new WindowsDesktopStartupRegistrar();
        var desktopSettings = _dataRoot.DesktopSettings().Load();
        var shouldStartHidden = IsStartupLaunch(e.Args)
                                && desktopSettings.LaunchAtStartup
                                && desktopSettings.SilentStartup;

        // Take ownership before the first await, while still on the dispatcher thread.
        _singleInstance = DesktopSingleInstance.TryAcquire(_dataRoot.RootPath);
        if (_singleInstance is null)
        {
            var activated = await DesktopSingleInstance.WaitForActivationAsync(
                token => TryActivateRunningInstanceAsync(_dataRoot, token),
                TimeSpan.FromSeconds(15)).ConfigureAwait(true);
            if (!activated && !shouldStartHidden)
            {
                System.Windows.MessageBox.Show(
                    "XKE 正在启动或暂时无法响应，请稍后重试。",
                    "XKE", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        // Keep HTTP activation for an already-running instance from before the mutex was introduced.
        if (await TryActivateRunningInstanceAsync(_dataRoot).ConfigureAwait(true))
        {
            Shutdown();
            return;
        }

        // Only the owner may replace stale discovery data; a waiting process must not clear it.
        _dataRoot.DesktopRuntime().Clear();

        MainWindow? window = null;
        _appUpdates = new AppUpdateService(CreateUpdateBackend(), TimeProvider.System,
            () => (window ?? throw new InvalidOperationException("桌面尚未就绪")).RequestUpdateRestart());
        if (_appUpdates.State.Status == "ready" && desktopSettings.AutoUpdateEnabled
            && _appUpdates.PrepareExit(automaticEnabled: true, restart: true))
        {
            Shutdown();
            return;
        }
        _webHostService = new XkeWebHostService(new XkeWebHostOptions
        {
            HostMode = "desktop-webview",
            CaptchaRecognizer = new Xke.DesktopHost.Captcha.DesktopCaptchaRecognizer(),
            AppUpdates = _appUpdates,
            RuntimeChannel = "dotnet8-desktop",
            DesktopWindowController = new DeferredDesktopWindowController(() => window),
            DesktopStartupRegistrar = startupRegistrar,
            DesktopRequestToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            DesktopNotifier = new WindowsDesktopNotifier(() => window),
        });

        window = new MainWindow(_webHostService, _dataRoot);
        MainWindow = window;
        if (shouldStartHidden)
        {
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.WindowState = System.Windows.WindowState.Minimized;
        }

        window.Show();
        await window.InitializeAsync(loadWebView: !shouldStartHidden).ConfigureAwait(true);
        if (shouldStartHidden)
        {
            window.EnterSilentStartupMode();
        }

        if (_webHostService.LaunchResult?.BaseUrl is { } baseUrl)
        {
            _dataRoot.DesktopRuntime().Save(ResolvePort(baseUrl), Environment.ProcessId);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        try
        {
            if (_singleInstance is not null)
            {
                _dataRoot?.DesktopRuntime().Clear(Environment.ProcessId);
            }

            if (_webHostService is not null)
            {
                // OnExit cannot be awaited by WPF. Stop off-context, then release on the
                // owning dispatcher thread only after the loopback listener has stopped.
                Task.Run(async () => await _webHostService.DisposeAsync().ConfigureAwait(false))
                    .GetAwaiter().GetResult();
            }
        }
        finally
        {
            _appUpdates?.Dispose();
            _singleInstance?.Dispose();
            _singleInstance = null;
            base.OnExit(e);
        }
    }

    private static async Task<bool> TryActivateRunningInstanceAsync(
        XkeDataRoot dataRoot,
        CancellationToken cancellationToken = default)
    {
        var instance = dataRoot.DesktopRuntime().Load();
        if (!instance.HasEndpoint)
        {
            return false;
        }

        using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://127.0.0.1:{instance.Port}"),
            Timeout = TimeSpan.FromSeconds(2),
        };

        try
        {
            using var health = await client.GetAsync("/api/health", cancellationToken).ConfigureAwait(false);
            if (!health.IsSuccessStatusCode)
            {
                return false;
            }

            using var activate = await client.PostAsJsonAsync("/api/desktop/activate", new { }, cancellationToken)
                .ConfigureAwait(false);
            if (!activate.IsSuccessStatusCode)
            {
                return false;
            }

            // Only treat the existing process as authoritative when it can surface a real desktop window.
            var payload = await activate.Content.ReadFromJsonAsync<ApiEnvelope>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return payload?.Data is JsonElement dataElement
                && dataElement.ValueKind == JsonValueKind.Object
                && dataElement.TryGetProperty("activated", out var activatedElement)
                && activatedElement.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return false;
        }
    }

    private static IAppUpdateBackend CreateUpdateBackend()
    {
        try { return new VelopackUpdateBackend(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceWarning("更新组件不可用，继续运行本地功能：{0}", ex.GetType().Name);
            return new DisabledAppUpdateBackend();
        }
    }

    private static int ResolvePort(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Port : 0;
    }

    private static bool IsStartupLaunch(IEnumerable<string> args)
    {
        return args.Any(arg => string.Equals(arg, WindowsDesktopStartupRegistrar.StartupArgument, StringComparison.OrdinalIgnoreCase));
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
