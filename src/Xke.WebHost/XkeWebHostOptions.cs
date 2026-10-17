namespace Xke.WebHost;

using Xke.Core.Auth.Jw;
using Xke.Core.Desktop;
using Xke.Core.Network;
using Xke.Core.Schedule;

public sealed class XkeWebHostOptions
{
    public Xke.Core.Releases.AppUpdateService? AppUpdates { get; init; }

    public IReadOnlyList<string> Urls { get; init; } = Array.Empty<string>();
    public string? WebRootPath { get; init; }
    public string? AuthDataRootPath { get; init; }
    public string RuntimeChannel { get; init; } = "dotnet8";
    public string HostMode { get; init; } = "standalone";
    public IReadOnlyList<string> AllowedFrontendOrigins { get; init; } = Array.Empty<string>();
    // Ephemeral native capability; never serialized or written to runtime files.
    public string? DesktopRequestToken { get; init; }
    public IDesktopNotifier? DesktopNotifier { get; init; }
    public bool EnableBackgroundTasks { get; init; } = true;
    public Func<string, string, DirectJwLoginProtocol>? JwLoginProtocolFactory { get; init; }
    public IJwScheduleClient? JwScheduleClient { get; init; }
    public IScheduleSnapshotSource? ScheduleSnapshotSource { get; init; }
    public ScheduleApiOptions? ScheduleApiOptions { get; init; }
    public HttpMessageHandler? NetworkHttpMessageHandler { get; init; }
    public CampusSelfServiceOptions? CampusSelfServiceOptions { get; init; }
    public Func<ICampusSelfServiceClient>? CampusSelfServiceClientFactory { get; init; }
    public INetworkCommandExecutor? NetworkCommandExecutor { get; init; }
    public IDesktopWindowController? DesktopWindowController { get; init; }
    public IDesktopStartupRegistrar? DesktopStartupRegistrar { get; init; }
}
