namespace Xke.Core.Notice;

public sealed class HttpNoticeRemoteSource : INoticeRemoteSource
{
    public const string DefaultBaseUrl = "https://xke-notice-1327163070.cos.ap-guangzhou.myqcloud.com/";

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;

    public HttpNoticeRemoteSource(HttpClient httpClient, string? baseUrl = null)
    {
        _httpClient = httpClient;
        _baseUri = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim(), UriKind.Absolute);
    }

    public async Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = (relativePath ?? string.Empty).Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return new NoticeRemoteContentResult(false, Array.Empty<byte>(), "path missing");
        }

        try
        {
            var uri = Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri
                : new Uri(_baseUri, normalizedPath);
            var content = await _httpClient.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
            return new NoticeRemoteContentResult(true, content, string.Empty);
        }
        catch (Exception ex)
        {
            return new NoticeRemoteContentResult(false, Array.Empty<byte>(), ex.Message);
        }
    }
}
