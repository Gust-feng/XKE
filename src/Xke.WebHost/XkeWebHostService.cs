using Microsoft.Extensions.DependencyInjection;

namespace Xke.WebHost;

public sealed class XkeWebHostService : IAsyncDisposable
{
    private readonly XkeWebHostOptions _options;
    private WebApplication? _application;

    public XkeWebHostService(XkeWebHostOptions? options = null)
    {
        _options = options ?? new XkeWebHostOptions();
    }

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
            RuntimeChannel = _options.RuntimeChannel,
            WebRootPath = _options.WebRootPath,
            AuthDataRootPath = _options.AuthDataRootPath,
            JwAuthClient = _options.JwAuthClient,
            JwScheduleClient = _options.JwScheduleClient,
            CourseClient = _options.CourseClient,
            NoticeRemoteSource = _options.NoticeRemoteSource,
            NoticeSigningPublicKeyPem = _options.NoticeSigningPublicKeyPem,
            NetworkHttpMessageHandler = _options.NetworkHttpMessageHandler,
            NetworkCommandExecutor = _options.NetworkCommandExecutor,
            DesktopWindowController = _options.DesktopWindowController,
            Urls = _options.Urls.Count > 0 ? _options.Urls : new[] { "http://127.0.0.1:0" },
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
}
