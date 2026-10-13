using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Xke.Contracts.Responses;

namespace Xke.Core.Network;

public interface ICampusSelfServiceClient : IDisposable
{
    Task<CampusSelfServiceSummary> GetSummaryAsync(string account, string password, CancellationToken cancellationToken);
}

public sealed class CampusSelfServiceException(string message) : Exception(message);

public sealed class CampusSelfServiceClient : ICampusSelfServiceClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private readonly int _timeoutSeconds;
    private bool _authenticated;

    // Each instance owns an isolated cookie jar; no portal client or persisted cookies.
    public CampusSelfServiceClient(CampusSelfServiceOptions options, HttpMessageHandler? handler = null)
    {
        if (!options.Configured) throw new ArgumentException("自助后台配置不可用", nameof(options));
        _baseUri = new Uri(options.Endpoint.TrimEnd('/') + "/");
        _timeoutSeconds = options.TimeoutSeconds;
        _http = new HttpClient(handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
        }) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        _http.DefaultRequestHeaders.Referrer = new Uri(_baseUri, "login/");
        _http.DefaultRequestHeaders.Add("Origin", _baseUri.GetLeftPart(UriPartial.Authority));
    }

    public async Task<CampusSelfServiceSummary> GetSummaryAsync(string account, string password, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
        try
        {
            if (_authenticated)
            {
                var existing = await ReadDashboardAsync(timeout.Token).ConfigureAwait(false);
                if (existing is not null) return existing;
                _authenticated = false;
            }
            await LoginAsync(account, password, timeout.Token).ConfigureAwait(false);
            var summary = await ReadDashboardAsync(timeout.Token).ConfigureAwait(false)
                ?? throw new CampusSelfServiceException("自助后台登录未完成");
            _authenticated = true;
            return summary;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _authenticated = false;
            throw new CampusSelfServiceException("自助后台连接超时");
        }
        catch (HttpRequestException)
        {
            _authenticated = false;
            throw new CampusSelfServiceException("暂时无法连接自助后台");
        }
    }

    private async Task LoginAsync(string account, string password, CancellationToken token)
    {
        // The server redirects /login to /login/. Use the observed canonical URL.
        using var page = await _http.GetAsync(new Uri(_baseUri, "login/"), token).ConfigureAwait(false);
        page.EnsureSuccessStatusCode();
        using var document = new HtmlParser().ParseDocument(await page.Content.ReadAsStringAsync(token).ConfigureAwait(false));
        var form = document.QuerySelector("form[action]");
        var action = form?.GetAttribute("action");
        // Tomcat may put the new session into the first form action. The cookie
        // jar already received it; do not propagate URL-encoded session IDs.
        action = action is null ? null : Regex.Replace(action, @";jsessionid=[^/?#;]*", "");
        if (form is null || !Uri.TryCreate(new Uri(_baseUri, "login/"), action, out var target)
            || target != new Uri(_baseUri, "login/verify"))
            throw new CampusSelfServiceException("自助后台登录页面已变化");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in form.QuerySelectorAll("input[name]:not([disabled])"))
        {
            var name = input.GetAttribute("name")!;
            if (input.GetAttribute("type") == "hidden" || name is "foo" or "bar" or "code")
                fields[name] = input.GetAttribute("value") ?? string.Empty;
        }
        fields["account"] = account;
        fields["password"] = password;

        // Even with the image hidden, the page initializes this login-session
        // endpoint before submit. Discard image bytes; do not solve/save a captcha.
        using var initialize = await _http.GetAsync(new Uri(_baseUri, "login/randomCode"), token).ConfigureAwait(false);
        initialize.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        using var response = await _http.SendAsync(request, token).ConfigureAwait(false);
        if (IsRedirect(response, "dashboard")) return;
        if (response.IsSuccessStatusCode)
            throw new CampusSelfServiceException("自助后台登录未完成，可能需要验证码");
        if (IsRedirect(response, "login/"))
            throw new CampusSelfServiceException("自助后台登录未完成，请检查校园网凭据或验证码要求");
        throw new CampusSelfServiceException("自助后台登录暂不可用");
    }

    private async Task<CampusSelfServiceSummary?> ReadDashboardAsync(CancellationToken token)
    {
        using var response = await _http.GetAsync(new Uri(_baseUri, "dashboard"), token).ConfigureAwait(false);
        if (IsRedirect(response, "login/") || IsRedirect(response, "login")
            || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        using var document = new HtmlParser().ParseDocument(html);
        if (document.QuerySelector("form input[name='password']") is not null) return null;
        return CampusSelfServiceParser.Parse(html);
    }

    private bool IsRedirect(HttpResponseMessage response, string path) =>
        response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.MovedPermanently
        && response.Headers.Location is { } location
        && new Uri(_baseUri, location) == new Uri(_baseUri, path);

    public void Dispose() => _http.Dispose();
}
