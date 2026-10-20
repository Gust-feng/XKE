using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xke.Contracts.Models;
using Xke.Core.Schedule;

namespace Xke.Core.Auth.Jw;

/// <summary>
/// Handles the current JW form-login protocol with a private cookie container.
/// Completed sessions transfer ownership to the JW client for protected storage.
/// </summary>
public sealed class DirectJwLoginProtocol : IDisposable
{
    public const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    // The form-login service is host-bound. Starting at the public IP is not
    // interchangeable with the JW domain because its JSESSIONID is host-only.
    private static readonly Uri LoginUri = new("http://222.243.161.213/");
    private readonly string _user;
    private readonly string _password;
    private readonly HttpClient _client;
    private readonly CookieContainer? _cookieContainer;
    private bool _ownsClient;
    private bool _transferred;

    public DirectJwLoginProtocol(string user, string password, HttpClient? client = null)
    {
        _user = (user ?? string.Empty).Trim();
        _password = password ?? string.Empty;
        if (client is null)
        {
            _cookieContainer = new CookieContainer();
            _client = CreateClient(_cookieContainer);
        }
        else
        {
            _client = client;
        }
        _ownsClient = client is null;
    }

    public async Task<string> BeginAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var page = await _client.GetAsync(LoginUri, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(page, "jw_login_page");

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(LoginUri, $"verifycode.servlet?t={Guid.NewGuid():N}"));
            request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            request.Headers.Referrer = LoginUri;
            using var captcha = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!captcha.IsSuccessStatusCode)
            {
                if (attempt == 0)
                {
                    continue;
                }

                EnsureSuccess(captcha, "jw_captcha");
            }

            var bytes = await captcha.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                continue;
            }

            var mediaType = captcha.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        }

        throw new JwScheduleException("教务验证码为空，请重新尝试登录。阶段=jw_captcha", 502);
    }

    public async Task CompleteAsync(string captchaCode, CancellationToken cancellationToken = default)
    {
        var code = (captchaCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new JwScheduleException("验证码不能为空", 400);
        }

        using var seedRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(LoginUri, "Logon.do?method=logon&flag=sess"));
        using var seedResponse = await _client.SendAsync(seedRequest, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(seedResponse, "jw_login_seed");
        var seed = await seedResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var encoded = EncodeCredentials(seed, _user, _password);

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("RANDOMCODE", code),
            new KeyValuePair<string, string>("encoded", encoded),
        ]);
        using var response = await _client.PostAsync(new Uri(LoginUri, "Logon.do?method=logon"), content, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, "jw_login_submit");
        var finalUri = response.RequestMessage?.RequestUri;
        var redirectedUri = response.Headers.Location is null ? null : AbsoluteUri(LoginUri, response.Headers.Location);
        var enteredWorkspace = (finalUri?.AbsolutePath ?? string.Empty).StartsWith("/jsxsd/", StringComparison.OrdinalIgnoreCase)
                               || (redirectedUri?.AbsolutePath ?? string.Empty).StartsWith("/jsxsd/", StringComparison.OrdinalIgnoreCase);
        if (!enteredWorkspace)
        {
            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw JwLoginException.FromResponse(html);
        }
    }

    public TraditionalJwClient TransferAuthenticatedSession()
    {
        if (_transferred)
        {
            throw new InvalidOperationException("JW protocol session has already been transferred.");
        }

        _transferred = true;
        _ownsClient = false;
        var client = new TraditionalJwClient(_user, _password, "protocol", new TraditionalJwOptions(), _client, ownsInjectedHttpClient: true, assumeJwSession: true);
        client.ImportStoredSession(ExportStoredSession());
        return client;
    }

    public JwSessionDocument ExportStoredSession()
    {
        var cookies = new List<JwSessionCookieDocument>();
        if (_cookieContainer is not null)
        {
            foreach (var uri in new[] { new Uri("http://222.243.161.213/"), new Uri("http://jw.hnsoftedu.com/jsxsd/") })
            {
                foreach (Cookie cookie in _cookieContainer.GetCookies(uri))
                {
                    if (cookie.Expired || string.IsNullOrWhiteSpace(cookie.Name) || string.IsNullOrWhiteSpace(cookie.Value))
                    {
                        continue;
                    }

                    cookies.Add(new JwSessionCookieDocument
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Host = cookie.Domain,
                        Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                    });
                }
            }
        }

        return new JwSessionDocument
        {
            User = _user,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
            Cookies = cookies
                .GroupBy(item => $"{item.Name}\u001f{item.Host}\u001f{item.Path}", StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
        };
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    public static string EncodeCredentials(string seedResponse, string user, string password)
    {
        var parts = (seedResponse ?? string.Empty).Trim().Split('#', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new JwScheduleException("教务登录随机编码材料无效。阶段=jw_login_seed", 502);
        }

        var source = $"{(user ?? string.Empty).Trim()}%%%{password ?? string.Empty}";
        var padding = parts[0];
        var indexes = parts[1];
        var builder = new StringBuilder(source.Length + padding.Length);
        for (var index = 0; index < source.Length; index++)
        {
            builder.Append(source[index]);
            if (index >= 20 || index >= indexes.Length || !char.IsDigit(indexes[index]))
            {
                continue;
            }

            var length = indexes[index] - '0';
            length = Math.Min(length, padding.Length);
            builder.Append(padding.AsSpan(0, length));
            padding = padding[length..];
        }

        return builder.ToString();
    }

    private static HttpClient CreateClient(CookieContainer cookieContainer)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            CookieContainer = cookieContainer,
            UseCookies = true,
        };
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        return client;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string stage)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new JwScheduleException($"教务协议请求失败。阶段={stage}, HTTP={(int)response.StatusCode}", 502);
        }
    }

    private static Uri AbsoluteUri(Uri baseUri, Uri value)
    {
        return value.IsAbsoluteUri ? value : new Uri(baseUri, value);
    }
}
