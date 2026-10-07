namespace Xke.Core.Update;

public sealed class LocalOnlyUpdatePackageDownloader : IUpdatePackageDownloader
{
    public Task DownloadAsync(
        Uri packageUri,
        string destinationPath,
        Func<long, long?, CancellationToken, Task> reportProgress,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("本地开发模式已禁用软件下载器。");
    }
}
