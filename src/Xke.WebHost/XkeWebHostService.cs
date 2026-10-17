using Microsoft.Extensions.DependencyInjection;

namespace Xke.WebHost;

public sealed class XkeWebHostService : IAsyncDisposable
{
    private const int DefaultDesktopLoopbackPort = 38443;
    private readonly XkeWebHostOptions _options;
    private WebApplication? _application;

    public XkeWebHostService(XkeWebHostOptions? options = null)
    {
        _options = options ?? new XkeWebHostOptions();
    }

    public Xke.Core.Releases.AppUpdateService? AppUpdates => _options.AppUpdates;

    public string? DesktopRequestToken => _options.DesktopRequestToken;

    public XkeWebHostLaunchResult? LaunchResult { get; private set; }

    public async Task<XkeWebHostLaunchResult> StartAsync(CancellationToken cancellationToken = default)
    {
        if (LaunchResult is not null)
        {
            return LaunchResult;
        }

        var launchOptions = new XkeWebHostOptions
        {
            HostMode = _options.HostMode,
            AppUpdates = _options.AppUpdates,
            AllowedFrontendOrigins = _options.AllowedFrontendOrigins,
            DesktopRequestToken = _options.DesktopRequestToken,
            DesktopNotifier = _options.DesktopNotifier,
            EnableBackgroundTasks = _options.EnableBackgroundTasks,
            RuntimeChannel = _options.RuntimeChannel,
            WebRootPath = _options.WebRootPath,
            AuthDataRootPath = _options.AuthDataRootPath,
            JwLoginProtocolFactory = _options.JwLoginProtocolFactory,
            JwScheduleClient = _options.JwScheduleClient,
            ScheduleSnapshotSource = _options.ScheduleSnapshotSource,
            ScheduleApiOptions = _options.ScheduleApiOptions,
            NetworkHttpMessageHandler = _options.NetworkHttpMessageHandler,
            CampusSelfServiceOptions = _options.CampusSelfServiceOptions,
            CampusSelfServiceClientFactory = _options.CampusSelfServiceClientFactory,
            NetworkCommandExecutor = _options.NetworkCommandExecutor,
            DesktopWindowController = _options.DesktopWindowController,
            DesktopStartupRegistrar = _options.DesktopStartupRegistrar,
            Urls = ResolveUrls(),
        };

        _application = XkeWebHostApplication.Create(Array.Empty<string>(), launchOptions);
        await _application.StartAsync(cancellationToken);

        var resolvedPaths = _application.Services.GetRequiredService<Xke.Core.Runtime.XkeResolvedPaths>();
        LaunchResult = new XkeWebHostLaunchResult(
            BaseUrl: XkeWebHostApplication.ResolveBaseUrl(_application),
            WebRootPath: resolvedPaths.WebRootPath,
            WebRootFound: resolvedPaths.HasWebRoot);

        return LaunchResult;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null)
        {
            LaunchResult = null;
            return;
        }

        await _application.StopAsync(cancellationToken);
        await _application.DisposeAsync();
        _application = null;
        LaunchResult = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private IReadOnlyList<string> ResolveUrls()
    {
        if (_options.Urls.Count > 0)
        {
            return _options.Urls;
        }

        return IsDesktopWebViewHostMode(_options.HostMode)
            ? [$"http://127.0.0.1:{DefaultDesktopLoopbackPort}"]
            : ["http://127.0.0.1:0"];
    }

    private static bool IsDesktopWebViewHostMode(string? hostMode)
    {
        return string.Equals(hostMode?.Trim(), "desktop-webview", StringComparison.OrdinalIgnoreCase);
    }
}
