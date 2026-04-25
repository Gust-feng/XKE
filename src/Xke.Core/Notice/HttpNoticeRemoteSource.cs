namespace Xke.Core.Notice;

using Xke.Core.CloudSync;

public sealed class HttpNoticeRemoteSource : INoticeRemoteSource
{
    public const string DefaultBaseUrl = "https://example.invalid/xke-cloud/";
    public const string BaseUrlEnvironmentVariable = "XKE_NOTICE_BASE_URL";

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;

    public HttpNoticeRemoteSource(HttpClient httpClient, string? baseUrl = null)
    {
        _httpClient = httpClient;
        _baseUri = new Uri(ResolveBaseUrl(baseUrl), UriKind.Absolute);
    }

    public static string ResolveBaseUrl(string? configured = null)
    {
        var privateOptions = PrivateCloudEndpointOptions.Load();
        return FirstNonEmpty(
            configured,
            Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable),
            Environment.GetEnvironmentVariable("XKE_CLOUD_NOTICE_BASE_URL"),
            privateOptions.NoticeBaseUrl,
            DefaultBaseUrl).Trim();
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

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
