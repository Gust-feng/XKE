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
using Xke.Storage;
using Xke.WebHost;
using Forms = global::System.Windows.Forms;

namespace Xke.DesktopHost;

public partial class MainWindow : Window, IDesktopWindowController
{
    private const string AppDisplayName = "XKE·软大课务";
    private readonly XkeWebHostService _webHostService;
    private readonly XkeDataRoot _dataRoot;
    private XkeWebHostLaunchResult? _launchResult;
    private Forms.NotifyIcon? _trayIcon;
    private Task? _webViewLoadTask;
    private bool _allowClose;

    public MainWindow(XkeWebHostService webHostService, XkeDataRoot dataRoot)
    {
        _webHostService = webHostService;
        _dataRoot = dataRoot;
        InitializeComponent();
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
        CopyAddressButton.IsEnabled = false;
        StatusText.Text = forceRestart ? "正在重启..." : "正在启动...";
        BaseUrlText.Text = "-";
        Browser.Visibility = Visibility.Collapsed;
        FallbackPanel.Visibility = Visibility.Visible;

        try
        {
            if (forceRestart)
            {
                await _webHostService.StopAsync().ConfigureAwait(true);
            }

            _launchResult = await _webHostService.StartAsync().ConfigureAwait(true);
            StatusText.Text = _launchResult.WebRootFound ? "运行中" : "运行中（缺少 web 根目录）";
            BaseUrlText.Text = _launchResult.BaseUrl;
            WebRootText.Text = _launchResult.WebRootPath ?? "未找到 web/index.html";
            DetailText.Text = _launchResult.WebRootFound
                ? "Host 已启动，正在初始化 WebView2..."
                : "Host 已启动，但还没有找到可用的 web 根目录。请确认仓库根目录下存在 web/index.html。";

            OpenBrowserButton.IsEnabled = true;
            CopyAddressButton.IsEnabled = true;

            if (_launchResult.WebRootFound)
            {
                var health = await ReadHostHealthAsync(_launchResult.BaseUrl).ConfigureAwait(true);
                DetailText.Text = BuildHostDiagnosticsText(health);
                if (loadWebView)
                {
                    await LoadWebViewAsync(_launchResult.BaseUrl, health?.FrontendBuild).ConfigureAwait(true);
                }
                else
                {
                    DetailText.Text = "Host 已启动，静默启动期间暂不加载 WebView2，打开窗口后会继续初始化前端。";
                }
            }
        }
        catch (Exception ex)
        {
            _launchResult = null;
            StatusText.Text = "启动失败";
            WebRootText.Text = "-";
            DetailText.Text = ex.Message;
            Browser.Visibility = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
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
            DetailText.Text = BuildHostDiagnosticsText(health);
            await LoadWebViewAsync(_launchResult.BaseUrl, health?.FrontendBuild).ConfigureAwait(true);
        }
        finally
        {
            _webViewLoadTask = null;
        }
    }

    private async Task LoadWebViewAsync(string baseUrl, string? frontendBuild)
    {
        try
        {
            var userDataFolder = Path.Combine(_dataRoot.DesktopDirectory, "webview-data");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder).ConfigureAwait(true);
            await Browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            await InjectDesktopBridgeAsync().ConfigureAwait(true);
            Browser.Source = BuildVersionedStartupUri(baseUrl, frontendBuild);
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 初始化失败";
            DetailText.Text = $"WebView2 不可用：{ex.Message}。可以使用系统浏览器打开当前地址。";
            Browser.Visibility = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
        }
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

    private static string BuildHostDiagnosticsText(HostHealthResponse? health)
    {
        if (health is null)
        {
            return "Host 已启动，未能读取前端诊断信息，仍将尝试初始化 WebView2。";
        }

        var appHash = health.StaticAssetHashes is not null
                      && health.StaticAssetHashes.TryGetValue("assets/app.js", out var hash)
            ? hash[..Math.Min(hash.Length, 12)]
            : "unknown";
        return $"Host 已启动，正在初始化 WebView2... 前端构建 {health.FrontendBuild}，app.js {appHash}";
    }

    private static Uri BuildVersionedStartupUri(string baseUrl, string? frontendBuild)
    {
        var build = string.IsNullOrWhiteSpace(frontendBuild) ? "unknown" : frontendBuild.Trim();
        var root = baseUrl.TrimEnd('/') + "/";
        var separator = root.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return new Uri($"{root}{separator}xke_build={Uri.EscapeDataString(build)}&xke_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
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
                start_window_drag: () => invoke('start_window_drag', []),
                start_update_install: (planPath) => invoke('start_update_install', [planPath])
              };
              window.dispatchEvent(new Event('pywebviewready'));
            })();
            """;
    }

    private async void Browser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
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
            "start_window_drag" => false,
            "start_update_install" => StartUpdateInstall(args.ElementAtOrDefault(0)),
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

    private Dictionary<string, object?> StartUpdateInstall(string? planPath)
    {
        var resolvedPlanPath = Path.GetFullPath(planPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(planPath) || !File.Exists(resolvedPlanPath))
        {
            throw new InvalidOperationException("安装计划不存在，请重新准备更新");
        }

        var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
        {
            throw new InvalidOperationException("无法定位当前主程序");
        }

        var runner = PrepareUpdaterRunner();
        Process.Start(BuildUpdaterStartInfo(runner, resolvedPlanPath, currentExe));

        _allowClose = true;
        _dataRoot.DesktopRuntime().Clear(Environment.ProcessId);
        Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
        return new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["runner"] = runner.ExecutablePath,
        };
    }

    private Dictionary<string, object?> BuildWindowState()
    {
        return new Dictionary<string, object?>
        {
            ["available"] = true,
            ["visible"] = IsVisible,
            ["frameless"] = false,
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

    private UpdaterRunner PrepareUpdaterRunner()
    {
        var source = ResolveUpdaterSource()
                     ?? throw new InvalidOperationException("未找到 Xke.Updater，请确认发布包包含更新器");
        var runnerDirectory = _dataRoot.Updates().ResolveRunnerDirectory(Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runnerDirectory);

        foreach (var file in Directory.EnumerateFiles(source.DirectoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            if (ShouldCopyUpdaterRuntimeFile(fileName))
            {
                File.Copy(file, Path.Combine(runnerDirectory, fileName), overwrite: true);
            }
        }

        var runnerExecutable = Path.Combine(runnerDirectory, source.FileName);
        if (!File.Exists(runnerExecutable))
        {
            throw new InvalidOperationException("更新器运行副本创建失败");
        }

        return new UpdaterRunner(runnerDirectory, runnerExecutable, source.IsDll);
    }

    private UpdaterSource? ResolveUpdaterSource()
    {
        foreach (var candidate in ResolveUpdaterCandidates(AppContext.BaseDirectory))
        {
            if (File.Exists(candidate))
            {
                return new UpdaterSource(
                    DirectoryPath: Path.GetDirectoryName(candidate)!,
                    FileName: Path.GetFileName(candidate),
                    IsDll: candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    private IEnumerable<string> ResolveUpdaterCandidates(string appDirectory)
    {
        yield return Path.Combine(appDirectory, "Xke.Updater.exe");
        yield return Path.Combine(appDirectory, "Xke.Updater.dll");

        if (string.IsNullOrWhiteSpace(_dataRoot.WorkspaceRoot))
        {
            yield break;
        }

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var output = Path.Combine(
                _dataRoot.WorkspaceRoot,
                "src",
                "Xke.Updater",
                "bin",
                configuration,
                "net8.0-windows");
            yield return Path.Combine(output, "Xke.Updater.exe");
            yield return Path.Combine(output, "Xke.Updater.dll");
        }
    }

    private static bool ShouldCopyUpdaterRuntimeFile(string fileName)
    {
        return fileName.StartsWith("Xke.", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessStartInfo BuildUpdaterStartInfo(UpdaterRunner runner, string planPath, string currentExe)
    {
        var startInfo = runner.IsDll
            ? new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = runner.DirectoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
            : new ProcessStartInfo
            {
                FileName = runner.ExecutablePath,
                WorkingDirectory = runner.DirectoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

        if (runner.IsDll)
        {
            startInfo.ArgumentList.Add(runner.ExecutablePath);
        }

        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--plan");
        startInfo.ArgumentList.Add(planPath);
        startInfo.ArgumentList.Add("--exe");
        startInfo.ArgumentList.Add(currentExe);
        return startInfo;
    }

    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            StatusText.Text = "前端加载失败";
            DetailText.Text = $"WebView2 导航失败：{e.WebErrorStatus}";
            Browser.Visibility = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
            return;
        }

        StatusText.Text = "运行中";
        DetailText.Text = "WebView2 前端已加载。";
        Browser.Visibility = Visibility.Visible;
        FallbackPanel.Visibility = Visibility.Collapsed;
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

        _trayIcon = new Forms.NotifyIcon
        {
            Text = AppDisplayName,
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        _trayIcon.ContextMenuStrip.Items.Add("打开 XKE", null, (_, _) => ActivateDesktopWindow());
        _trayIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => ExitApplication());
        _trayIcon.DoubleClick += (_, _) => ActivateDesktopWindow();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !ShouldCloseToTray())
        {
            _dataRoot.DesktopRuntime().Clear(Environment.ProcessId);
            DisposeTrayIcon();
            return;
        }

        // Keep the local WebHost alive when the user chose tray-close semantics.
        e.Cancel = true;
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
            DetailText.Text = $"打开浏览器失败：{ex.Message}";
        }
    }

    private void CopyAddress_Click(object sender, RoutedEventArgs e)
    {
        if (_launchResult is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(_launchResult.BaseUrl);
        DetailText.Text = "已复制本地地址。";
    }

    protected override async void OnClosed(EventArgs e)
    {
        DisposeTrayIcon();
        if (_allowClose || !ShouldCloseToTray())
        {
            _dataRoot.DesktopRuntime().Clear(Environment.ProcessId);
            await _webHostService.StopAsync().ConfigureAwait(false);
        }

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
    }

    private sealed record UpdaterSource(string DirectoryPath, string FileName, bool IsDll);

    private sealed record UpdaterRunner(string DirectoryPath, string ExecutablePath, bool IsDll);
}
