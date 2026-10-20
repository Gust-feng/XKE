using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Xke.Contracts.Responses;
using Xke.Core.Desktop;
using Xke.Core.Releases;
using Xke.Storage;
using Xke.WebHost;
using Drawing = global::System.Drawing;
using Forms = global::System.Windows.Forms;

namespace Xke.DesktopHost;

public partial class MainWindow : Window, IDesktopWindowController
{
    private const string AppDisplayName = "XKE";
    private readonly XkeWebHostService _webHostService;
    private readonly XkeDataRoot _dataRoot;
    private XkeWebHostLaunchResult? _launchResult;
    private Forms.NotifyIcon? _trayIcon;
    private Drawing.Icon? _trayIconImage;
    private Task? _webViewLoadTask;
    private bool _allowClose;
    private bool _stopping;
    private bool _hostStopped;
    private bool _requestBoundaryInstalled;
    private bool _presentWhenReady;
    private bool _webViewReady;
    private TaskCompletionSource? _frontendReady;
    private readonly System.Windows.Threading.DispatcherTimer _startupWatchdog;

    public MainWindow(XkeWebHostService webHostService, XkeDataRoot dataRoot)
    {
        _webHostService = webHostService;
        _dataRoot = dataRoot;
        InitializeComponent();
        // Match the product canvas while WebView2 creates its first document.
        Browser.DefaultBackgroundColor = Drawing.Color.FromArgb(0xF2, 0xF1, 0xEC);
        _startupWatchdog = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _startupWatchdog.Tick += (_, _) => ShowErrorState(
            "界面启动超时", "本地界面暂未就绪，可以退出后重新打开。", allowBrowserActions: _launchResult is not null);
    }

    public void PrepareStartupPresentation(bool showWhenReady)
    {
        _presentWhenReady = showWhenReady;
        // Create the HWND for WebView2 without presenting an empty window or taskbar item.
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
        Show();
    }

    public async Task InitializeAsync(bool loadWebView = true)
    {
        await StartHostAsync(forceRestart: false, loadWebView: loadWebView).ConfigureAwait(true);
    }

    DesktopActivatePayload IDesktopWindowController.Activate()
    {
        return ActivateDesktopWindow();
    }

    public DesktopActivatePayload ActivateDesktopWindow()
    {
        DesktopActivatePayload ActivateCore()
        {
            if (!_webViewReady && ErrorPanel.Visibility != Visibility.Visible)
            {
                _presentWhenReady = true;
                if (!IsVisible) PrepareStartupPresentation(showWhenReady: true);
                EnsureWebViewLoaded();
                return new DesktopActivatePayload(Activated: true, Visible: false, WindowState: BuildWindowState());
            }
            ShowActivated = true;
            ShowInTaskbar = true;
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            base.Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            EnsureWebViewLoaded();
            PublishWindowState();
            return new DesktopActivatePayload(
                Activated: true,
                Visible: IsVisible,
                WindowState: BuildWindowState());
        }

        return Dispatcher.CheckAccess() ? ActivateCore() : Dispatcher.Invoke(ActivateCore);
    }

    public void EnterSilentStartupMode()
    {
        EnsureTrayIcon();
        ShowInTaskbar = false;
        Hide();
        PublishWindowState();
    }

    private async Task StartHostAsync(bool forceRestart, bool loadWebView = true)
    {
        RetryButton.IsEnabled = false;
        OpenBrowserButton.IsEnabled = false;
        ShowInitializationState();

        try
        {
            if (forceRestart)
            {
                await _webHostService.StopAsync().ConfigureAwait(true);
            }

            _launchResult = await _webHostService.StartAsync().ConfigureAwait(true);
            if (!_launchResult.WebRootFound)
            {
                ShowErrorState(
                    "未找到前端资源",
                    "当前发布内容缺少 web/index.html，无法打开内嵌界面。",
                    allowBrowserActions: false);
                return;
            }

            var health = await ReadHostHealthAsync(_launchResult.BaseUrl).ConfigureAwait(true);
            if (loadWebView)
            {
                await LoadWebViewAsync(_launchResult.BaseUrl, health?.FrontendBuild).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _launchResult = null;
            ShowErrorState(
                forceRestart ? "重新加载失败" : "界面打开失败",
                ex.Message,
                allowBrowserActions: false);
        }
        finally
        {
            RetryButton.IsEnabled = true;
        }
    }

    private void EnsureWebViewLoaded()
    {
        if (_launchResult is null
            || !_launchResult.WebRootFound
            || Browser.CoreWebView2 is not null
            || _webViewLoadTask is not null)
        {
            return;
        }

        _webViewLoadTask = LoadWebViewForCurrentHostAsync();
    }

    private async Task LoadWebViewForCurrentHostAsync()
    {
        if (_launchResult is null)
        {
            return;
        }

        try
        {
            var health = await ReadHostHealthAsync(_launchResult.BaseUrl).ConfigureAwait(true);
            ShowInitializationState();
            await LoadWebViewAsync(_launchResult.BaseUrl, health?.FrontendBuild).ConfigureAwait(true);
        }
        finally
        {
            _webViewLoadTask = null;
        }
    }

    private async Task LoadWebViewAsync(string baseUrl, string? frontendBuild)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _frontendReady = ready;
        _webViewReady = false;
        try
        {
            var userDataFolder = Path.Combine(_dataRoot.DesktopDirectory, "webview-data");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder).ConfigureAwait(true);
            await Browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            // WebView2 handles CSS app-region title-bar gestures through the native window.
            Browser.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = true;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            InstallRequestBoundary();
            await InjectDesktopBridgeAsync().ConfigureAwait(true);
            Browser.Source = BuildVersionedStartupUri(baseUrl, frontendBuild);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(true);
            _webViewReady = true;
            ShowWebViewState();
            if (_presentWhenReady)
            {
                _presentWhenReady = false;
                ActivateDesktopWindow();
            }
        }
        catch (Exception ex)
        {
            ShowErrorState(
                ex is TimeoutException ? "界面启动超时" : "内嵌界面初始化失败",
                ex is TimeoutException ? "本地界面暂未就绪，请重新加载。" : $"WebView2 不可用：{ex.Message}",
                allowBrowserActions: _launchResult is not null);
        }
        finally
        {
            if (ReferenceEquals(_frontendReady, ready)) _frontendReady = null;
        }
    }

    private bool IsRuntimeOrigin(string address)
        => _launchResult is not null && Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && Uri.TryCreate(_launchResult.BaseUrl, UriKind.Absolute, out var runtime)
            && uri.Scheme == runtime.Scheme && uri.Host == runtime.Host && uri.Port == runtime.Port;

    private void InstallRequestBoundary()
    {
        if (_requestBoundaryInstalled || Browser.CoreWebView2 is null) return;
        _requestBoundaryInstalled = true;
        Browser.CoreWebView2.NavigationStarting += (_, e) => { if (!IsRuntimeOrigin(e.Uri)) e.Cancel = true; };
        Browser.CoreWebView2.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (!e.IsUserInitiated || !Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || !AppRelease.IsProjectPage(uri)) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { Notify(AppDisplayName, "无法打开浏览器，请稍后重试。"); }
        };
        Browser.CoreWebView2.AddWebResourceRequestedFilter(_launchResult!.BaseUrl.TrimEnd('/') + "/api/*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceRequested += (_, e) =>
        {
            if (IsRuntimeOrigin(e.Request.Uri) && new Uri(e.Request.Uri).AbsolutePath.StartsWith("/api/", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(_webHostService.DesktopRequestToken))
                e.Request.Headers.SetHeader(LocalApiAccessPolicy.DesktopTokenHeaderName, _webHostService.DesktopRequestToken);
        };
    }

    private static async Task<HostHealthResponse?> ReadHostHealthAsync(string baseUrl)
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(5),
            };
            return await client.GetFromJsonAsync<HostHealthResponse>("/api/health").ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
    }

    private static Uri BuildVersionedStartupUri(string baseUrl, string? frontendBuild)
    {
        var build = string.IsNullOrWhiteSpace(frontendBuild) ? "unknown" : frontendBuild.Trim();
        var root = baseUrl.TrimEnd('/') + "/";
        var separator = root.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return new Uri($"{root}{separator}xke_build={Uri.EscapeDataString(build)}&xke_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
    }

    private void ShowInitializationState()
    {
        Browser.Visibility = Visibility.Hidden;
        _webViewReady = false;
        if (_presentWhenReady) _startupWatchdog.Start();
        // On retry keep the actual error surface and exit action, never a bare loading shell.
        FallbackExitButton.Visibility = ErrorPanel.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowErrorState(string title, string detail, bool allowBrowserActions)
    {
        _startupWatchdog.Stop();
        Browser.Visibility = Visibility.Hidden;
        FallbackExitButton.Visibility = Visibility.Visible;
        ErrorTitleText.Text = string.IsNullOrWhiteSpace(title) ? "界面加载失败" : title;
        ErrorDetailText.Text = string.IsNullOrWhiteSpace(detail)
            ? "请重试，或暂时用浏览器打开本地界面。"
            : detail;
        ErrorPanel.Visibility = Visibility.Visible;
        OpenBrowserButton.IsEnabled = allowBrowserActions && _launchResult is not null;
        if (_presentWhenReady)
        {
            _presentWhenReady = false;
            ActivateDesktopWindow();
        }
    }

    private void ShowWebViewState()
    {
        _startupWatchdog.Stop();
        ErrorPanel.Visibility = Visibility.Collapsed;
        FallbackExitButton.Visibility = Visibility.Collapsed;
        Browser.Visibility = Visibility.Visible;
    }

    private async Task InjectDesktopBridgeAsync()
    {
        if (Browser.CoreWebView2 is null)
        {
            return;
        }

        // Keep the browser-side contract close to pywebview so the existing desktop JS can be reused.
        Browser.CoreWebView2.WebMessageReceived -= Browser_WebMessageReceived;
        Browser.CoreWebView2.WebMessageReceived += Browser_WebMessageReceived;
        await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildBridgeScript()).ConfigureAwait(true);
    }

    private static string BuildBridgeScript()
    {
        return """
            (() => {
              if (window.__xkeDotNetDesktopBridgeInstalled) return;
              window.__xkeDotNetDesktopBridgeInstalled = true;
              let seq = 0;
              const pending = new Map();
              function invoke(method, args) {
                const id = `xke-${++seq}`;
                chrome.webview.postMessage({ kind: 'xke-desktop-bridge', id, method, args: Array.from(args || []) });
                return new Promise((resolve, reject) => {
                  pending.set(id, { resolve, reject });
                  window.setTimeout(() => {
                    if (!pending.has(id)) return;
                    pending.delete(id);
                    reject(new Error(`Desktop bridge timeout: ${method}`));
                  }, 8000);
                });
              }
              chrome.webview.addEventListener('message', (event) => {
                const message = event.data || {};
                if (message.kind === 'xke-desktop-bridge-result' && pending.has(message.id)) {
                  const entry = pending.get(message.id);
                  pending.delete(message.id);
                  if (message.ok === false) entry.reject(new Error(message.error || 'Desktop bridge failed'));
                  else entry.resolve(message.result);
                }
                if (message.kind === 'xke-window-state') {
                  window.dispatchEvent(new CustomEvent('xke-window-state', { detail: message.state || {} }));
                }
              });
              window.pywebview = window.pywebview || {};
              window.pywebview.platform = 'edgechromium';
              window.pywebview.api = {
                notify: (title, message) => invoke('notify', [title, message]),
                show_window: () => invoke('show_window', []),
                minimize_window: () => invoke('minimize_window', []),
                toggle_maximize_window: () => invoke('toggle_maximize_window', []),
                close_window: () => invoke('close_window', []),
                get_window_state: () => invoke('get_window_state', []),
                content_ready: () => invoke('content_ready', []),
                start_window_drag: () => invoke('start_window_drag', []),
              };
              window.dispatchEvent(new Event('pywebviewready'));
            })();
            """;
    }

    private async void Browser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsRuntimeOrigin(e.Source) || _stopping) return;
        string id = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "xke-desktop-bridge")
            {
                return;
            }

            id = root.GetProperty("id").GetString() ?? string.Empty;
            var method = root.GetProperty("method").GetString() ?? string.Empty;
            var args = root.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Array
                ? argsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
            // Only the current document may release startup; a previous reload can finish late.
            if (method == "content_ready" && !string.Equals(e.Source, Browser.Source?.AbsoluteUri, StringComparison.Ordinal)) return;
            var result = HandleBridgeCall(method, args);
            await PostBridgeResultAsync(id, ok: true, result, string.Empty).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await PostBridgeResultAsync(id, ok: false, null, ex.Message).ConfigureAwait(true);
        }
    }

    private object? HandleBridgeCall(string method, IReadOnlyList<string> args)
    {
        return method switch
        {
            "notify" => Notify(args.ElementAtOrDefault(0), args.ElementAtOrDefault(1)),
            "show_window" => ActivateDesktopWindow(),
            "minimize_window" => MinimizeWindow(),
            "toggle_maximize_window" => ToggleMaximizeWindow(),
            "close_window" => CloseFromBridge(),
            "get_window_state" => BuildWindowState(),
            "content_ready" => _frontendReady?.TrySetResult() ?? false,
            "start_window_drag" => false,
            _ => throw new InvalidOperationException($"Unsupported desktop bridge method: {method}"),
        };
    }

    private Task PostBridgeResultAsync(string id, bool ok, object? result, string error)
    {
        if (Browser.CoreWebView2 is null || string.IsNullOrWhiteSpace(id))
        {
            return Task.CompletedTask;
        }

        var payload = JsonSerializer.Serialize(new
        {
            kind = "xke-desktop-bridge-result",
            id,
            ok,
            result,
            error,
        });
        Browser.CoreWebView2.PostWebMessageAsJson(payload);
        return Task.CompletedTask;
    }

    private Dictionary<string, object?> Notify(string? title, string? message)
    {
        EnsureTrayIcon();
        _trayIcon?.ShowBalloonTip(
            4000,
            string.IsNullOrWhiteSpace(title) ? AppDisplayName : title,
            message ?? string.Empty,
            Forms.ToolTipIcon.Info);
        return new Dictionary<string, object?> { ["ok"] = true };
    }

    internal bool ShowLessonReminder(string title, string message)
    {
        if (_stopping || _allowClose || Dispatcher.HasShutdownStarted) return false;
        Notify(title, message);
        return true;
    }

    private Dictionary<string, object?> MinimizeWindow()
    {
        WindowState = WindowState.Minimized;
        PublishWindowState();
        return BuildWindowState();
    }

    private Dictionary<string, object?> ToggleMaximizeWindow()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        PublishWindowState();
        return BuildWindowState();
    }

    private bool CloseFromBridge()
    {
        Close();
        return true;
    }



    private Dictionary<string, object?> BuildWindowState()
    {
        return new Dictionary<string, object?>
        {
            ["available"] = true,
            ["visible"] = IsVisible,
            ["frameless"] = true,
            ["state"] = WindowState.ToString().ToLowerInvariant(),
            ["maximized"] = WindowState == WindowState.Maximized,
            ["minimized"] = WindowState == WindowState.Minimized,
            ["title"] = AppDisplayName,
        };
    }

    private void PublishWindowState()
    {
        if (Browser.CoreWebView2 is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            kind = "xke-window-state",
            state = BuildWindowState(),
        });
        Browser.CoreWebView2.PostWebMessageAsJson(payload);
    }











    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            if (_frontendReady is { } pending)
            {
                pending.TrySetException(new InvalidOperationException($"WebView2 导航失败：{e.WebErrorStatus}"));
                return;
            }
            ShowErrorState(
                "内嵌界面加载失败",
                $"WebView2 导航失败：{e.WebErrorStatus}",
                allowBrowserActions: _launchResult is not null);
            return;
        }

        // Navigation completion alone does not mean React and account recovery have committed.
        PublishWindowState();
    }

    private bool ShouldCloseToTray()
    {
        return _dataRoot.DesktopSettings().Load().CloseToTray;
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _trayIconImage = LoadTrayIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Text = AppDisplayName,
            Icon = _trayIconImage,
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        _trayIcon.ContextMenuStrip.Items.Add($"打开 {AppDisplayName}", null, (_, _) => ActivateDesktopWindow());
        _trayIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => ExitApplication());
        _trayIcon.DoubleClick += (_, _) => ActivateDesktopWindow();
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        var streamInfo = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute));
        if (streamInfo?.Stream is null)
        {
            return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
        }

        using var stream = streamInfo.Stream;
        using var icon = new Drawing.Icon(stream);
        return (Drawing.Icon)icon.Clone();
    }

    public void RequestUpdateRestart()
        => Dispatcher.BeginInvoke(new Action(ExitApplication));

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    private void FallbackExit_Click(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_hostStopped) return;
        e.Cancel = true;
        if (_stopping) return;
        if (_allowClose || !ShouldCloseToTray())
        {
            _stopping = true;
            _allowClose = true;
            DisposeTrayIcon();
            try
            {
                // Keep the dispatcher alive while cancellation drains background work.
                await _webHostService.StopAsync().ConfigureAwait(true);
                _webHostService.AppUpdates?.PrepareExit(_dataRoot.DesktopSettings().Load().AutoUpdateEnabled);
            }
            catch (Exception ex)
            {
                _stopping = false;
                ShowErrorState("退出失败", ex.Message, allowBrowserActions: false);
                return;
            }
            _hostStopped = true;
            _dataRoot.DesktopRuntime().Clear(Environment.ProcessId);
            // StopAsync may complete synchronously. Never reenter Close inside Closing.
            _ = Dispatcher.BeginInvoke(new Action(Close));
            return;
        }

        // Keep the local WebHost alive when the user chose tray-close semantics.
        EnsureTrayIcon();
        ShowInTaskbar = false;
        Hide();
        Notify(AppDisplayName, "软件仍在运行，可从托盘重新打开。");
        PublishWindowState();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        PublishWindowState();
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        PublishWindowState();
    }

    private void OpenCurrentAddressInBrowser()
    {
        if (_launchResult is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _launchResult.BaseUrl,
            UseShellExecute = true,
        });
    }

    private async void RetryHost_Click(object sender, RoutedEventArgs e)
    {
        await StartHostAsync(forceRestart: true).ConfigureAwait(true);
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OpenCurrentAddressInBrowser();
        }
        catch (Exception ex)
        {
            ShowErrorState(
                "浏览器打开失败",
                $"无法在系统浏览器中打开本地界面：{ex.Message}",
                allowBrowserActions: _launchResult is not null);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _startupWatchdog.Stop();
        _frontendReady?.TrySetCanceled();
        DisposeTrayIcon();
        Browser.Dispose();
        base.OnClosed(e);
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
        _trayIconImage?.Dispose();
        _trayIconImage = null;
    }


}
