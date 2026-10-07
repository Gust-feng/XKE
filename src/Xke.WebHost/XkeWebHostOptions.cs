namespace Xke.WebHost;

using Xke.Core.Auth.Jw;
using Xke.Core.Course;
using Xke.Core.Desktop;
using Xke.Core.Feedback;
using Xke.Core.Network;
using Xke.Core.Notice;
using Xke.Core.Schedule;
using Xke.Core.Telemetry;
using Xke.Core.Update;

public sealed class XkeWebHostOptions
{
    public IReadOnlyList<string> Urls { get; init; } = Array.Empty<string>();
    public string? WebRootPath { get; init; }
    public string? AuthDataRootPath { get; init; }
    public string RuntimeChannel { get; init; } = "dotnet8";
    public string HostMode { get; init; } = "standalone";
    public IJwAuthClient? JwAuthClient { get; init; }
    // Test-only seam for the retired mobile authentication client. Production
    // hosts always use the JW form protocol.
    public bool UseLegacyMobileAuthentication { get; init; }
    public bool UseLegacyMobileScheduleForTests { get; init; }
    public bool LocalOnly { get; init; }
    public IJwScheduleClient? JwScheduleClient { get; init; }
    public ICourseClient? CourseClient { get; init; }
    public INoticeRemoteSource? NoticeRemoteSource { get; init; }
    public string? NoticeSigningPublicKeyPem { get; init; }
    public ITelemetryTransport? TelemetryTransport { get; init; }
    public string? TelemetryCloudEndpoint { get; init; }
    public IFeedbackTransport? FeedbackTransport { get; init; }
    public string? FeedbackCloudEndpoint { get; init; }
    public HttpMessageHandler? NetworkHttpMessageHandler { get; init; }
    public INetworkCommandExecutor? NetworkCommandExecutor { get; init; }
    public IDesktopWindowController? DesktopWindowController { get; init; }
    public IDesktopStartupRegistrar? DesktopStartupRegistrar { get; init; }
    public IUpdatePackageDownloader? UpdatePackageDownloader { get; init; }
    public IUpdateVersionProvider? UpdateVersionProvider { get; init; }
    public string? MainExecutablePath { get; init; }
}
