namespace Xke.Core.Update;

public interface IUpdatePackageDownloader
{
    Task DownloadAsync(
        Uri packageUri,
        string destinationPath,
        Func<long, long?, CancellationToken, Task> reportProgress,
        CancellationToken cancellationToken = default);
}

public sealed class HttpUpdatePackageDownloader : IUpdatePackageDownloader
{
    private readonly HttpClient _httpClient;

    public HttpUpdatePackageDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task DownloadAsync(
        Uri packageUri,
        string destinationPath,
        Func<long, long?, CancellationToken, Task> reportProgress,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[1024 * 128];
        long received = 0;
        await reportProgress(received, totalBytes, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            await reportProgress(received, totalBytes, cancellationToken).ConfigureAwait(false);
        }
    }
}
