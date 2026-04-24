namespace Xke.WebHost;

using Xke.Core.Auth.Jw;
using Xke.Core.Course;
using Xke.Core.Desktop;
using Xke.Core.Network;
using Xke.Core.Notice;
using Xke.Core.Schedule;

public sealed class XkeWebHostOptions
{
    public IReadOnlyList<string> Urls { get; init; } = Array.Empty<string>();
    public string? WebRootPath { get; init; }
    public string? AuthDataRootPath { get; init; }
    public string RuntimeChannel { get; init; } = "dotnet8";
    public string HostMode { get; init; } = "standalone";
    public IJwAuthClient? JwAuthClient { get; init; }
    public IJwScheduleClient? JwScheduleClient { get; init; }
    public ICourseClient? CourseClient { get; init; }
    public INoticeRemoteSource? NoticeRemoteSource { get; init; }
    public string? NoticeSigningPublicKeyPem { get; init; }
    public HttpMessageHandler? NetworkHttpMessageHandler { get; init; }
    public INetworkCommandExecutor? NetworkCommandExecutor { get; init; }
    public IDesktopWindowController? DesktopWindowController { get; init; }
}
