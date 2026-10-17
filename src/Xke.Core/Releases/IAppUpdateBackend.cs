namespace Xke.Core.Releases;

/// <summary>The desktop owns the updater. Core never downloads or executes a replacement binary itself.</summary>
public interface IAppUpdateBackend
{
    bool IsInstalled { get; }
    string? CurrentVersion { get; }
    string? PendingVersion { get; }
    Task<string?> CheckAsync(CancellationToken cancellationToken);
    Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken);
    void ApplyOnExit(bool restart);
}

public sealed class DisabledAppUpdateBackend : IAppUpdateBackend
{
    public bool IsInstalled => false;
    public string? CurrentVersion => null;
    public string? PendingVersion => null;
    public Task<string?> CheckAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    public Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken) => Task.CompletedTask;
    public void ApplyOnExit(bool restart) => throw new InvalidOperationException("此运行方式不支持安装更新");
}
