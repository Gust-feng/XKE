namespace Xke.WebHost;

public sealed record XkeWebHostLaunchResult(
    string BaseUrl,
    string? WebRootPath,
    bool WebRootFound
);
