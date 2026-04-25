namespace Xke.Core.Notice;

public sealed class NoticeServiceOptions
{
    public string AppRoot { get; init; } = AppContext.BaseDirectory;
    public string? PublicKeyPem { get; init; }
    public string DefaultChannel { get; init; } = "beta";
    public string CloudFeedPath { get; init; } = "cloud-feed.json";
    public string IndexPath { get; init; } = "index.json";
    public string IndexSignaturePath { get; init; } = "index.sig";
}
