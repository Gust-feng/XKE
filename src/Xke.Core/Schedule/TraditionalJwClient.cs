using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Contracts.Models;
using Xke.Core.Auth.Jw;
using Xke.Storage;

namespace Xke.Core.Schedule;

public sealed class TraditionalJwClient : IDisposable
{
    private const string BrowserUserAgent = DirectJwLoginProtocol.BrowserUserAgent;
    private static readonly TimeSpan AutomatedPreloginRequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly Uri SsoBaseUri = new("http://sso.hnsoftedu.com/");
    private static readonly Uri PortalValidateUri = new("http://my.hnsoftedu.com/cas/validate");
    private static readonly Uri JwSsoUri = new("http://jw.hnsoftedu.com/sso.jsp");
    private static readonly Uri JwMainPageUri = new("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV.jsp");
    private static readonly Uri JwMainNewPageUri = new("http://jw.hnsoftedu.com/jsxsd/framework/xsMainV_new.jsp?t1=1");
    private static readonly Uri JwScheduleLoadUri = new("http://jw.hnsoftedu.com/jsxsd/framework/mainV_index_loadkb.jsp");
    private static readonly (string Key, string Value)[] UniversalCsrfPairs =
    [
        ("FzgxPikIetYDlXZM4lRG9taclVDa99lB", "7964f321f00366a3a287a133dd307ed0"),
        ("12hAvHecmBe641DDOE03RfhiRWeofamC", "909eb6bf83ae3c206eaf246eac4c5f39"),
        ("16dxCuuoiz6PIoKvWNmqLVKYbzDU5hs4", "16ef4a2b8cc8e6a31c69e1d29552e010"),
        ("GjfXC72lltiP3V86J503kTVHVxEcxHc0", "79451cdd46c2d5cc26484a59645aac4b"),
        ("GF8Dgcq3ehzhmOOQiNByyBUjetOUlCTp", "e843d23bad119b1bfdd4e4709b681749"),
        ("H3jj1HimvY9AtZJ3o9gRyPhwaVHML90x", "9ccd5b62b9ee4538b32c9b17b70441ac"),
        ("SkJBuqpnLo851NeU3mRF53TzyamYuaZy", "508bef8f2924e68f5d4db16ab5320266"),
        ("xlmu2ASbw9Rv3oAP6y46SuYsUJT0ab4F", "af3448a026b89db55fa4a83f11e68415"),
        ("um7WtXecppolI900VmEcnsekzqLnJAy6", "ce8e39fce5c9108ab67c7d937975a2d6"),
        ("y2CNeMXeT7BijjHCoHeTDqDynxZpOnyd", "95aa358f14a63906387db98bf2a7ef0a"),
        ("dEDkExVfhFvbDFWDqg6izvoViSc4c18y", "45b9c8d221cc0bac75b38d42ad5a35ad"),
        ("0gMes0g7aQ233ppmjiMDPNaS9z63afY7", "25f78352cb13a61df36344fb814bfda8"),
        ("EWGNl2R0icufEGHZOeSr0twma2nyk3qa", "b2a464504f3d97bc5423d8a99d88fea0"),
        ("5qLrlpCfyZjYZvgzc7fZiAzGCeljztLa", "c5a445a92b49a6a11ad6f20017c8574c"),
        ("UWD8dALsRwZcugPvWbllbv1cYfaEvLA2", "af57a6ce185db4e2d928bc10de1c66cd"),
        ("yqbiv9OWnWpowS4DaOAs2mzzdzftCPwC", "135c354ef90cdd5da9387f55c0d87d61"),
        ("Qd8WA00X9XYfscUbCnAo5UGEo4tFBKRC", "70e0299983f10ba3ac558513048aa8f6"),
        ("tBSIBpdkfBOmSnLmEvCcJvKdYSNCTqg5", "aec5b7db9b7d1b324c42d3376d824f5f"),
        ("CaSCu7DIcePyBUUTJlRpifrjbOAp3zwK", "e12d91d96f3608033f3c2d670604e3d5"),
        ("L0vQBa4tpuDzJt6W3fU75W97DwXK5ZVV", "46aa021af44afba03a045ab853216dd8"),
        ("SF9oEBeXL0dXlgkr5cDae4KOrGdmnbcY", "28c83b14011bd1b09e1c8704d61af34d"),
        ("SaOifY5RaAg0Xhtimz8AYrNaH5QjhhJx", "a6670de5ea48772599c30fa6d57c920d"),
        ("KSrz7NuVpTPWspAoBSIuU6Uw4YD1TYnZ", "d3b4b113531a0be28f23b56cbbd25ca9"),
        ("O4Y4nlja2MS8T8HVlJpP1lrLTIo46tkN", "d1d2bd390db15ed9debea3468100176f"),
        ("LS13FkznRVNABkAzOp108xnFQq1xdtLE", "65fb6f45b0ac83591013f90a22a3a6f5"),
        ("CBzAPLWjam0ZFLdsX3xUAGj4UfbZP4EN", "df973dc92f12e6ef82c5bbdd05e5540b"),
        ("Tb84lAKNz18Fi2OTJPs4jz1FhuoYBu0L", "6e9a0bda8730d77b58b46a8976d9f771"),
        ("QdZ5F7mW2QrfRIGHF4pMBcNH3UwJb0yF", "589c33184e18616d24e7c10d165c03da"),
        ("BTvi7hMH4Bdh6QQsLsLbxhGhd4KN46a2", "5144d0b5f4d7bb2ff12c18b1008ab5e0"),
    ];

    private readonly string _user;
    private readonly string _password;
    private readonly TraditionalJwOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly bool _assumeJwSession;
    private readonly CookieContainer _cookies = new();
    private readonly HashSet<string> _observedCookieScopes = new(StringComparer.OrdinalIgnoreCase);
    private TraditionalJwLoginPageContext? _loginPage;

    public TraditionalJwClient(
        string user,
        string password,
        string preloginMode,
        TraditionalJwOptions? options = null,
        HttpClient? httpClient = null,
        bool ownsInjectedHttpClient = false,
        bool assumeJwSession = false)
    {
        _user = (user ?? string.Empty).Trim();
        _password = password ?? string.Empty;
        _ = preloginMode;
        _options = options ?? TraditionalJwOptions.CreateDefault();
        _httpClient = httpClient ?? CreateDefaultHttpClient(_options.Timeout);
        _ownsHttpClient = httpClient is null || ownsInjectedHttpClient;
        _assumeJwSession = assumeJwSession;
    }

    public async Task<TraditionalJwCaptchaChallenge> BeginLoginAsync(CancellationToken cancellationToken = default)
    {
        var page = await GetLoginPageAsync(cancellationToken).ConfigureAwait(false);
        await PreparePreloginBootstrapAsync(page, cancellationToken).ConfigureAwait(false);
        var captcha = await FetchCaptchaImageAsync(page, cancellationToken).ConfigureAwait(false);
        _loginPage = page;
        return new TraditionalJwCaptchaChallenge(captcha, _options.ChallengeExpiresInSeconds);
    }

    public async Task<JwScheduleFetchResult> CompleteLoginAndFetchScheduleAsync(
        string captchaCode,
        ScheduleRequest request,
        TraditionalJwScheduleHtmlParser parser,
        CancellationToken cancellationToken = default)
    {
        var page = _loginPage ?? throw new JwScheduleException("教务系统登录上下文已失效，请重新刷新课表", statusCode: 409);
        await LoginPortalAsync(page, captchaCode, cancellationToken).ConfigureAwait(false);
        await BootstrapJwSessionAsync(cancellationToken).ConfigureAwait(false);
        _loginPage = null;
        return await FetchScheduleAsync(request, parser, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JwScheduleFetchResult> FetchScheduleAsync(
        ScheduleRequest request,
        TraditionalJwScheduleHtmlParser parser,
        CancellationToken cancellationToken = default)
    {
        if (!HasJwSessionCookie())
        {
            throw new TraditionalJwSessionExpiredException("传统教务会话未完成 JW bootstrap，请重新输入验证码");
        }

        if (!string.IsNullOrWhiteSpace(request.AcademicTerm))
        {
            var week = ScheduleService.ParseWeekNumber(request.ManualWeek);
            TraditionalJwScheduleParams? currentContext = null;
            if (week is null)
            {
                currentContext = await GetScheduleParamsAsync(new ScheduleRequest(), cancellationToken).ConfigureAwait(false);
                if (request.AcademicTerm != currentContext.TermLabel)
                    throw new JwScheduleException("历史学期的单周查询需要 manual_week，请指定周次或使用学期接口", 400);
                week = ScheduleService.ParseWeekNumber(currentContext.CurrentWeekLabel);
            }
            if (week is null) throw new JwScheduleException("教务没有提供当前周次", 400);
            var page = await FetchSemesterPageAsync(request.AcademicTerm, week.Value.ToString(), cancellationToken).ConfigureAwait(false);
            return parser.Parse(page.Html, new ScheduleRequest { Mode = "manual", ManualWeek = week.Value.ToString(), AcademicTerm = page.Term, TermLabel = page.Term })
                with { TermLabel = page.Term, CurrentWeekNumber = currentContext is null ? null : week, AvailableWeeks = currentContext?.AvailableWeeks ?? [], WeekDates = new Dictionary<string, string>() };
        }
        var fetched = await FetchScheduleHtmlAsync(request, cancellationToken).ConfigureAwait(false);
        var parseRequest = new ScheduleRequest
        {
            Mode = request.Mode,
            ManualWeek = fetched.Params.WeekLabel,
            CookieRq = fetched.Params.Rq,
            TermLabel = request.TermLabel,
            AcademicTerm = request.AcademicTerm,
        };
        var parsed = parser.Parse(fetched.Html, parseRequest);
        return parsed with
        {
            WeekLabel = fetched.Params.WeekLabel,
            CurrentWeekNumber = ScheduleService.ParseWeekNumber(fetched.Params.CurrentWeekLabel),
            ViewedWeekNumber = ScheduleService.ParseWeekNumber(fetched.Params.WeekLabel),
            AvailableWeeks = fetched.Params.AvailableWeeks,
            RawPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "jw",
                ["parser"] = "traditional_jw_html",
                ["rq"] = fetched.Params.Rq,
                ["sjms_value"] = fetched.Params.SjmsValue,
                ["term"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = fetched.Params.TermLabel,
                    ["academic_term"] = fetched.Params.TermLabel,
                },
                ["notes"] = parsed.RawPayload.GetValueOrDefault("notes"),
                ["supplementary_courses"] = parsed.RawPayload.GetValueOrDefault("supplementary_courses"),
                ["row_count"] = parsed.Rows.Count,
                ["warning_count"] = parsed.DataWarnings.Count,
            },
        };
    }

    public async Task<JwScheduleFetchResult> FetchSemesterScheduleAsync(string academicTerm, TraditionalJwScheduleHtmlParser parser,
        CancellationToken cancellationToken = default)
    {
        var page = await FetchSemesterPageAsync(academicTerm, "", cancellationToken).ConfigureAwait(false);
        var parsed = parser.ParseSemester(page.Html, page.Term);
        parsed.RawPayload["available_terms"] = page.Terms;
        parsed.RawPayload["mode"] = page.Mode;
        return parsed with { TermLabel = page.Term };
    }

    private async Task<(string Html, string Term, IReadOnlyList<Dictionary<string, object?>> Terms, string Mode)> FetchSemesterPageAsync(
        string academicTerm, string week, CancellationToken cancellationToken)
    {
        EnsureJwSession();
        var uri = new Uri(JwMainPageUri, "/jsxsd/xskb/xskb_list.do");
        async Task<string> Read(Uri address)
        {
            using var response = await SendAsync(HttpMethod.Get, address,
                new Dictionary<string, string> { ["Referer"] = JwMainNewPageUri.ToString() }, null, cancellationToken, "jw_semester").ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new JwScheduleException($"学期课表请求失败，状态码: {(int)response.StatusCode}");
            JwHtml.EnsureAuthenticated(body);
            return body;
        }
        var html = await Read(uri).ConfigureAwait(false);
        using var index = JwHtml.Parse(html);
        var terms = JwScheduleContextParser.Options(index.QuerySelector("#xnxq01id"));
        if (terms.Count == 0) throw new JwScheduleException("学期课表页面缺少学期目录");
        var current = terms.FirstOrDefault(t => t["selected"] is true) ?? terms[0];
        var term = string.IsNullOrWhiteSpace(academicTerm) ? current["value"]?.ToString() ?? "" : academicTerm.Trim();
        if (!terms.Any(t => t["value"]?.ToString() == term)) throw new JwScheduleException("指定学期不在教务目录中", 404);
        var modes = JwScheduleContextParser.Options(index.QuerySelector("#kbjcmsid"));
        var mode = (modes.FirstOrDefault(m => m["selected"] is true) ?? modes.FirstOrDefault())?.GetValueOrDefault("value")?.ToString() ?? "";
        if (mode.Length == 0) throw new JwScheduleException("学期课表页面缺少节次模式");
        if (term != current["value"]?.ToString() || week.Length > 0)
            html = await Read(AddQuery(uri, new Dictionary<string, string> { ["xnxq01id"] = term, ["zc"] = week, ["kbjcmsid"] = mode })).ConfigureAwait(false);
        using var returned = JwHtml.Parse(html);
        var returnedTerm = JwScheduleContextParser.Options(returned.QuerySelector("#xnxq01id")).FirstOrDefault(t => t["selected"] is true);
        if (returnedTerm?.GetValueOrDefault("value")?.ToString() != term)
            throw new JwScheduleException("教务返回的学期与请求不一致");
        if (week.Length > 0 && JwScheduleContextParser.Options(returned.QuerySelector("#zc")).FirstOrDefault(w => w["selected"] is true)?.GetValueOrDefault("value")?.ToString() != week)
            throw new JwScheduleException("教务返回的周次与请求不一致");
        return (html, term, JwScheduleContextParser.Options(returned.QuerySelector("#xnxq01id")), mode);
    }

    public async Task<StudentProfileDocument> FetchStudentProfileAsync(CancellationToken cancellationToken = default)
    {
        EnsureJwSession();
        using var response = await SendAsync(HttpMethod.Get, new Uri(JwMainPageUri, "/jsxsd/grxx/xsxx"),
            new Dictionary<string, string> { ["Referer"] = JwMainPageUri.ToString() }, null, cancellationToken,
            stage: "jw_profile").ConfigureAwait(false);
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new JwScheduleException($"学生资料请求失败，状态码: {(int)response.StatusCode}");
        return ParseStudentProfile(html, _user);
    }

    public bool HasBootstrappedJwSession => HasJwSessionCookie();

    public static TraditionalJwClient FromStoredSession(
        string user,
        string password,
        JwSessionDocument document,
        TraditionalJwOptions? options = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        };
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = (options ?? TraditionalJwOptions.CreateDefault()).Timeout,
        };
        var result = new TraditionalJwClient(user, password, "protocol", options, client, ownsInjectedHttpClient: true, assumeJwSession: true);
        result.ImportStoredSession(document);
        return result;
    }

    public JwSessionDocument ExportStoredSession()
    {
        var cookies = new List<JwSessionCookieDocument>();
        foreach (var uri in new[]
        {
            new Uri("http://222.243.161.213/"),
            new Uri("http://jw.hnsoftedu.com/jsxsd/"),
        })
        {
            foreach (Cookie cookie in _cookies.GetCookies(uri))
            {
                if (cookie.Expired || string.IsNullOrWhiteSpace(cookie.Name) || string.IsNullOrWhiteSpace(cookie.Value))
                {
                    continue;
                }

                if (!cookies.Any(item => string.Equals(item.Name, cookie.Name, StringComparison.Ordinal)
                                         && string.Equals(item.Host, cookie.Domain, StringComparison.OrdinalIgnoreCase)
                                         && string.Equals(item.Path, cookie.Path, StringComparison.Ordinal)))
                {
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
            Cookies = cookies,
        };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateDefaultHttpClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout,
        };
    }

    private async Task<TraditionalJwLoginPageContext> GetLoginPageAsync(CancellationToken cancellationToken)
    {
        var loginUri = AddQuery(new Uri(SsoBaseUri, "login"), new Dictionary<string, string>
        {
            ["service"] = PortalValidateUri.ToString(),
            ["sn"] = "undefined",
        });
        using var response = await SendAsync(
            HttpMethod.Get,
            loginUri,
            new Dictionary<string, string>
            {
                ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
                ["Upgrade-Insecure-Requests"] = "1",
            },
            content: null,
            cancellationToken,
            stage: "login_page").ConfigureAwait(false);
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new JwScheduleException($"SSO 登录页请求失败，状态码: {(int)response.StatusCode}");
        }

        var execution = ExtractTagValue(html, "login-page-flowkey");
        var crypto = ExtractTagValue(html, "login-croypto");
        var captchaUrl = ExtractTagValue(html, "captcha-url");
        var redirectUri = ExtractTagValue(html, "redirect-uri");
        if (string.IsNullOrWhiteSpace(execution) || string.IsNullOrWhiteSpace(crypto))
        {
            throw new JwScheduleException("SSO 登录页缺少 execution 或 login-croypto，无法继续登录");
        }

        return new TraditionalJwLoginPageContext(response.RequestMessage?.RequestUri?.ToString() ?? loginUri.ToString(), execution, crypto, captchaUrl, redirectUri);
    }

    private async Task PreparePreloginBootstrapAsync(TraditionalJwLoginPageContext page, CancellationToken cancellationToken)
    {
        await RunAutomatedPreloginBootstrapAsync(page, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAutomatedPreloginBootstrapAsync(TraditionalJwLoginPageContext page, CancellationToken cancellationToken)
    {
        var sequence = BuildDynamicPreloginSequence(page.LoginUrl, _user);
        var successes = 0;
        var failures = 0;
        foreach (var request in sequence)
        {
            var requestWithReferer = request with
            {
                Headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase)
                {
                    ["Referer"] = page.LoginUrl,
                },
            };
            try
            {
                using var content = CreateTextContent(requestWithReferer);
                using var response = await SendAsync(
                    new HttpMethod(requestWithReferer.Method),
                    new Uri(requestWithReferer.Url, UriKind.Absolute),
                    requestWithReferer.Headers,
                    content,
                    cancellationToken,
                    stage: "prelogin_auto",
                    requestTimeout: AutomatedPreloginRequestTimeout).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    successes++;
                }
                else
                {
                    failures++;
                }
            }
            catch (HttpRequestException)
            {
                failures++;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }
            catch (JwScheduleException ex) when (ex.StatusCode == 504)
            {
                failures++;
            }
        }

        if (successes == 0 && failures > 0)
        {
            throw new JwScheduleException("SSO 自动预登录失败。阶段=prelogin_failed, 所有预登录请求均未成功", statusCode: 409);
        }
    }

    private static HttpContent? CreateTextContent(TraditionalPreloginRequest request)
    {
        return string.Equals(request.Method, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase)
            ? new StringContent(request.Body ?? string.Empty, Encoding.UTF8)
            : null;
    }

    private async Task<string> FetchCaptchaImageAsync(TraditionalJwLoginPageContext page, CancellationToken cancellationToken)
    {
        var captchaPath = string.IsNullOrWhiteSpace(page.CaptchaUrl) ? "api/captcha/generate/DEFAULT" : page.CaptchaUrl;
        var captchaUri = AbsoluteUri(SsoBaseUri, captchaPath);
        using var response = await SendAsync(
            HttpMethod.Get,
            captchaUri,
            new Dictionary<string, string>
            {
                ["Accept"] = "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8",
                ["Referer"] = page.LoginUrl,
            },
            content: null,
            cancellationToken,
            stage: "captcha_image").ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || bytes.Length == 0)
        {
            throw new JwScheduleException($"验证码获取失败，状态码: {(int)response.StatusCode}");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            mediaType = "image/png";
        }

        return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
    }

    private async Task LoginPortalAsync(TraditionalJwLoginPageContext page, string captchaCode, CancellationToken cancellationToken)
    {
        var cleanedCaptcha = (captchaCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedCaptcha))
        {
            throw new JwScheduleException("验证码不能为空", statusCode: 400);
        }

        var encryptedPassword = EncryptPassword(page.CryptoBase64);
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("username", _user),
            new KeyValuePair<string, string>("type", "UsernamePassword"),
            new KeyValuePair<string, string>("_eventId", "submit"),
            new KeyValuePair<string, string>("geolocation", string.Empty),
            new KeyValuePair<string, string>("execution", page.Execution),
            new KeyValuePair<string, string>("captcha_code", cleanedCaptcha),
            new KeyValuePair<string, string>("captcha_code", cleanedCaptcha),
            new KeyValuePair<string, string>("croypto", page.CryptoBase64),
            new KeyValuePair<string, string>("password", encryptedPassword),
        ]);
        using var response = await SendAsync(
            HttpMethod.Post,
            new Uri(SsoBaseUri, "login"),
            new Dictionary<string, string>
            {
                ["Cache-Control"] = "max-age=0",
                ["Origin"] = SsoBaseUri.ToString().TrimEnd('/'),
                ["Referer"] = page.LoginUrl,
                ["Upgrade-Insecure-Requests"] = "1",
                ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            },
            content,
            cancellationToken,
            stage: "portal_login").ConfigureAwait(false);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        var hasLocation = !string.IsNullOrWhiteSpace(location);
        var hasTicket = hasLocation && location.Contains("ticket=", StringComparison.OrdinalIgnoreCase);
        if (response.StatusCode != HttpStatusCode.Redirect || !hasTicket)
        {
            throw new JwScheduleException(
                $"SSO 登录失败，请检查账号密码或验证码。阶段=portal_login, HTTP={(int)response.StatusCode}, has_location={hasLocation}, has_ticket={hasTicket}",
                statusCode: 400);
        }

        await FollowRedirectsAsync(AbsoluteUri(new Uri(page.LoginUrl), location), "portal_redirect", cancellationToken).ConfigureAwait(false);
    }

    private async Task BootstrapJwSessionAsync(CancellationToken cancellationToken)
    {
        using (var initial = await SendAsync(HttpMethod.Get, JwSsoUri, new Dictionary<string, string>(), content: null, cancellationToken, stage: "jw_sso_entry").ConfigureAwait(false))
        {
            _ = initial.StatusCode;
        }

        var serviceUri = AddQuery(new Uri(SsoBaseUri, "login"), new Dictionary<string, string>
        {
            ["service"] = JwSsoUri.ToString(),
        });
        using var response = await SendAsync(
            HttpMethod.Get,
            serviceUri,
            new Dictionary<string, string>
            {
                ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
                ["Upgrade-Insecure-Requests"] = "1",
            },
            content: null,
            cancellationToken,
            stage: "jw_ticket").ConfigureAwait(false);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        var hasLocation = !string.IsNullOrWhiteSpace(location);
        var hasTicket = hasLocation && location.Contains("ticket=", StringComparison.OrdinalIgnoreCase);
        if (response.StatusCode != HttpStatusCode.Redirect || !hasTicket)
        {
            var hasObservedTgc = HasCookie(new Uri(SsoBaseUri, "/"), "SOURCEID_TGC") || HasObservedCookieName("SOURCEID_TGC");
            throw new JwScheduleException(
                $"未能通过门户登录态为教务系统换取 ticket。阶段=jw_ticket, HTTP={(int)response.StatusCode}, has_location={hasLocation}, has_ticket={hasTicket}, observed_SOURCEID_TGC={hasObservedTgc}. {BuildCookieObservationSummary()}",
                statusCode: 400);
        }

        await FollowRedirectsAsync(AbsoluteUri(serviceUri, location), "jw_redirect", cancellationToken).ConfigureAwait(false);
        if (!HasJwSessionCookie())
        {
            throw new JwScheduleException(
                $"JW ticket 跳转完成，但未拿到 /jsxsd 路径下的 JSESSIONID，教务课表接口仍不可用。阶段=jw_bootstrap. {BuildCookieObservationSummary()}",
                statusCode: 400);
        }
    }

    private async Task<TraditionalJwFetchedSchedule> FetchScheduleHtmlAsync(ScheduleRequest request, CancellationToken cancellationToken)
    {
        var parameters = await GetScheduleParamsAsync(request, cancellationToken).ConfigureAwait(false);
        var requestUri = AddQuery(JwScheduleLoadUri, new Dictionary<string, string>
        {
            ["rq"] = parameters.Rq,
            ["sjmsValue"] = parameters.SjmsValue,
        });
        using var response = await SendAsync(
            HttpMethod.Get,
            requestUri,
            new Dictionary<string, string>
            {
                ["X-Requested-With"] = "XMLHttpRequest",
                ["Accept"] = "text/html, */*; q=0.01",
                ["Referer"] = JwMainNewPageUri.ToString(),
            },
            content: null,
            cancellationToken,
            stage: "jw_schedule_html").ConfigureAwait(false);
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new JwScheduleException($"课表请求失败，状态码: {(int)response.StatusCode}");
        }

        if (LooksLikeLoginPage(html))
        {
            throw new TraditionalJwSessionExpiredException("课表请求返回登录页，当前 JW 会话已失效");
        }

        return new TraditionalJwFetchedSchedule(parameters, html);
    }

    private void EnsureJwSession()
    {
        if (!HasJwSessionCookie())
        {
            throw new TraditionalJwSessionExpiredException("传统教务会话未完成 JW bootstrap，不能读取课程预览");
        }
    }

    private async Task<TraditionalJwScheduleParams> GetScheduleParamsAsync(ScheduleRequest request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
            ["Referer"] = "http://jw.hnsoftedu.com/",
            ["Upgrade-Insecure-Requests"] = "1",
        };
        using (var main = await SendAsync(HttpMethod.Get, JwMainPageUri, headers, content: null, cancellationToken, stage: "jw_main").ConfigureAwait(false))
        {
            _ = main.StatusCode;
        }

        using var response = await SendAsync(HttpMethod.Get, JwMainNewPageUri, headers, content: null, cancellationToken, stage: "jw_schedule_params").ConfigureAwait(false);
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new JwScheduleException($"打开首页课表页失败，状态码: {(int)response.StatusCode}");
        }

        if (LooksLikeLoginPage(html))
        {
            throw new TraditionalJwSessionExpiredException("首页课表页返回登录页，当前 JW 会话已失效");
        }

        return ExtractScheduleParams(html, request);
    }

    private static TraditionalJwScheduleParams ExtractScheduleParams(string html, ScheduleRequest request)
        => JwScheduleContextParser.ParseHome(html, request);

    private async Task FollowRedirectsAsync(Uri startUri, string stage, CancellationToken cancellationToken)
    {
        var current = startUri;
        for (var hop = 0; hop < 10; hop++)
        {
            using var response = await SendAsync(HttpMethod.Get, current, new Dictionary<string, string>(), content: null, cancellationToken, stage: stage).ConfigureAwait(false);
            var location = response.Headers.Location?.ToString();
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            current = AbsoluteUri(current, location);
        }

        throw new JwScheduleException($"重定向次数过多，最后停在: {current.GetLeftPart(UriPartial.Path)}");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        IReadOnlyDictionary<string, string> headers,
        HttpContent? content,
        CancellationToken cancellationToken,
        string stage,
        TimeSpan? requestTimeout = null)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = content,
        };
        if (!HasHeader(headers, "User-Agent"))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        }

        if (!HasHeader(headers, "Accept-Language"))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        }

        if (!HasHeader(headers, "DNT"))
        {
            request.Headers.TryAddWithoutValidation("DNT", "1");
        }
        var cookieHeader = _cookies.GetCookieHeader(uri);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        foreach (var pair in headers)
        {
            if (string.Equals(pair.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content is not null)
            {
                request.Content.Headers.Remove(pair.Key);
                request.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
                continue;
            }

            if (string.Equals(pair.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var effectiveTimeout = requestTimeout ?? _options.Timeout;
        timeoutCts.CancelAfter(effectiveTimeout);
        try
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token).ConfigureAwait(false);
            CaptureCookies(uri, response);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && uri.AbsolutePath.StartsWith("/jsxsd/", StringComparison.OrdinalIgnoreCase))
            {
                response.Dispose();
                throw new TraditionalJwSessionExpiredException("教务请求返回401，当前JW会话已失效");
            }
            return response;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(stage, method, uri, effectiveTimeout);
        }
        catch (TimeoutException)
        {
            throw BuildTimeoutException(stage, method, uri, effectiveTimeout);
        }
    }

    private static JwScheduleException BuildTimeoutException(string stage, HttpMethod method, Uri uri, TimeSpan timeout)
    {
        return new JwScheduleException(
            $"传统教务请求超时。阶段={stage}, timeout={timeout.TotalSeconds:0.#}s, {method.Method} {uri.Host}{uri.AbsolutePath}",
            statusCode: 504);
    }

    private static bool HasHeader(IReadOnlyDictionary<string, string> headers, string name)
    {
        return headers.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
    }

    private void CaptureCookies(Uri uri, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            ObserveCookie(uri, value);
            TrySetCookie(uri, value);
        }
    }

    private void ObserveCookie(Uri uri, string header)
    {
        var firstPart = header.Split(';', 2, StringSplitOptions.TrimEntries)[0];
        var equalsIndex = firstPart.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return;
        }

        var name = firstPart[..equalsIndex].Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var domain = ExtractCookieAttribute(header, "Domain");
        if (string.IsNullOrWhiteSpace(domain))
        {
            domain = uri.Host;
        }

        var path = ExtractCookieAttribute(header, "Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        _observedCookieScopes.Add($"{name}@{domain}{path}");
    }

    private void TrySetCookie(Uri uri, string header)
    {
        try
        {
            _cookies.SetCookies(uri, header);
        }
        catch (CookieException)
        {
            var simplified = string.Join("; ", header
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !part.StartsWith("SameSite=", StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(simplified))
            {
                try
                {
                    _cookies.SetCookies(uri, simplified);
                }
                catch (CookieException)
                {
                }
            }
        }
    }

    private bool HasCookie(Uri uri, string name)
    {
        return _cookies.GetCookies(uri)
            .Cast<Cookie>()
            .Any(cookie => string.Equals(cookie.Name, name, StringComparison.Ordinal));
    }

    private bool HasObservedCookieName(string name)
    {
        return _observedCookieScopes.Any(scope => scope.StartsWith($"{name}@", StringComparison.OrdinalIgnoreCase));
    }

    private string BuildCookieObservationSummary()
    {
        if (_observedCookieScopes.Count == 0)
        {
            return "未观察到 Set-Cookie。";
        }

        return $"已观察到 Cookie 名称/作用域: {string.Join(", ", _observedCookieScopes.Order(StringComparer.OrdinalIgnoreCase))}";
    }

    private static string ExtractCookieAttribute(string header, string attributeName)
    {
        foreach (var part in header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var equalsIndex = part.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var name = part[..equalsIndex].Trim();
            if (string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return part[(equalsIndex + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    private bool HasJwSessionCookie()
    {
        if (_assumeJwSession)
        {
            return true;
        }
        return HasCookie(new Uri("http://jw.hnsoftedu.com/jsxsd/"), "JSESSIONID");
    }

    public void ImportStoredSession(JwSessionDocument document)
    {
        foreach (var item in document.Cookies)
        {
            if (string.IsNullOrWhiteSpace(item.Name)
                || string.IsNullOrWhiteSpace(item.Value)
                || string.IsNullOrWhiteSpace(item.Host)
                || !IsJwCookieHost(item.Host))
            {
                continue;
            }

            try
            {
                _cookies.Add(new Cookie(item.Name, item.Value, string.IsNullOrWhiteSpace(item.Path) ? "/" : item.Path, item.Host));
            }
            catch (CookieException)
            {
            }
        }
    }

    private static bool IsJwCookieHost(string host)
    {
        var normalized = (host ?? string.Empty).Trim().TrimStart('.');
        return normalized.Equals("222.243.161.213", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("jw.hnsoftedu.com", StringComparison.OrdinalIgnoreCase);
    }

    private string EncryptPassword(string cryptoBase64)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(cryptoBase64);
        }
        catch (FormatException)
        {
            throw new JwScheduleException("SSO 登录页返回的 login-croypto 不是有效 Base64", statusCode: 400);
        }

        if (key.Length != 8)
        {
            throw new JwScheduleException("SSO 登录页返回的 DES key 长度无效", statusCode: 400);
        }

        using var des = DES.Create();
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.PKCS7;
        des.Key = key;
        using var encryptor = des.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(_password);
        return Convert.ToBase64String(encryptor.TransformFinalBlock(plain, 0, plain.Length));
    }

    private static List<TraditionalPreloginRequest> BuildDynamicPreloginSequence(string loginUrl, string user)
    {
        var csrfIndex = 0;
        (string Key, string Value) NextCsrf()
        {
            if (csrfIndex >= UniversalCsrfPairs.Length)
            {
                return UniversalCsrfPairs[0];
            }

            return UniversalCsrfPairs[csrfIndex++];
        }

        TraditionalPreloginRequest Post(string url, string body, bool hasCrypto = false)
        {
            var csrf = NextCsrf();
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Csrf-Key"] = csrf.Key,
                ["Csrf-Value"] = csrf.Value,
                ["Content-Type"] = "application/json",
                ["Accept"] = hasCrypto ? "application/json, text/plain, */*" : "*/*",
                ["Origin"] = SsoBaseUri.ToString().TrimEnd('/'),
                ["Referer"] = loginUrl,
            };
            if (hasCrypto)
            {
                headers["hasCrypto"] = "true";
            }

            return new TraditionalPreloginRequest("POST", url, headers, body);
        }

        TraditionalPreloginRequest Get(string url)
        {
            var csrf = NextCsrf();
            return new TraditionalPreloginRequest(
                "GET",
                url,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Csrf-Key"] = csrf.Key,
                    ["Csrf-Value"] = csrf.Value,
                    ["Accept"] = "application/json, text/plain, */*",
                    ["Referer"] = loginUrl,
                },
                string.Empty);
        }

        var dictConfigUrl = new Uri(SsoBaseUri, "linkid/protected/api/dictconfig/get").ToString();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        return
        [
            Post(dictConfigUrl, "[\"pc.login.page.config.type\"]"),
            Post(dictConfigUrl, "ER7XScTKA67uL/TvQeSVys90ES/F/Ffv", hasCrypto: true),
            Post(dictConfigUrl, "[\"pc.self.page.config\"]"),
            Get(new Uri(SsoBaseUri, $"public/cas-login/assets/i18n/zh-CN.json?{ts}").ToString()),
            Get(new Uri(SsoBaseUri, $"public/deploy/domain/domain.json?{ts}").ToString()),
            Post(dictConfigUrl, "ER7XScTKA67uL/TvQeSVys90ES/F/Ffv", hasCrypto: true),
            Post(dictConfigUrl, "[\"create.internationalization.template\"]"),
            Get(new Uri(SsoBaseUri, $"public/deploy/domain/domain.json?{ts}").ToString()),
            Post(dictConfigUrl, "[\"phone.login.page.config\",\"pad.login.page.config\",\"pc.login.page.config\",\"pc.self.page.config\",\"pc.admin.page.config\",\"frontend.system.config\",\"school.custom.config\",\"phone.self.header.background\",\"pc.login.page.config.edit\",\"pad.login.page.config.edit\",\"pc.login.page.config.type\",\"pc.login.page.new.config\",\"pc.login.page.new.config.edit\",\"qudao.pc.login.page.new.config\"]"),
            Get(new Uri(SsoBaseUri, $"api/service/protected/get/name?{ts}").ToString()),
            Post(dictConfigUrl, "[\"phone.self.function\"]"),
            Post(dictConfigUrl, "[\"multi-industry-key-word\"]"),
            Get(new Uri(SsoBaseUri, $"linkid/protected/api/aggregate/authmethod/username/dictconfig/get?{ts}").ToString()),
            Get(new Uri(SsoBaseUri, $"linkid/protected/api/aggregate/authmethod/usernames/input/config/get?{ts}").ToString()),
            Post(new Uri(SsoBaseUri, "linkid/api/protected/partner/dictconfig/get").ToString(), "[\"partner.register.loginpage.show.switch\"]"),
            Post(dictConfigUrl, "[\"social.personal.register.switch\"]"),
            Post(dictConfigUrl, "[\"login.account.activate\"]"),
            Post(dictConfigUrl, "[\"big.single.config\"]"),
            Post(new Uri(SsoBaseUri, "linkid/api/protected/visitor/dictconfig/get").ToString(), "[\"visitor.identity.select\"]"),
            Get(new Uri(SsoBaseUri, $"linkid/api/aggregate/identitycategory/protected/all/get?{ts}").ToString()),
            Post(new Uri(SsoBaseUri, "linkid/api/protected/visitor/dictconfig/get").ToString(), "[\"visitor.register.loginpage.show.switch\"]"),
            Get(new Uri(SsoBaseUri, $"linkid/protected/api/portlet/find/publish?{ts}").ToString()),
            Get(new Uri(SsoBaseUri, $"api/protected/auth/method/getActive?{ts}").ToString()),
            Get(new Uri(SsoBaseUri, $"api/protected/wechat/checkEqualUser?{ts}").ToString()),
            Post(dictConfigUrl, "[\"pc.login.page.config\"]"),
            Post(dictConfigUrl, "[\"login.password.placeholder\"]"),
            Get(new Uri(SsoBaseUri, $"api/protected/user/findCaptchaCount/{Uri.EscapeDataString(user)}?{ts}").ToString()),
            Post(new Uri(SsoBaseUri, "linkid/protected/api/aggregate/securitystrategy/passwordcontrol/config/get/byscene").ToString(), "{\"scene\":\"password.recover.config\"}"),
            Post(dictConfigUrl, "[\"show.last.login.name\"]"),
            Post(dictConfigUrl, "[\"visitor.register.switch\"]"),
            Post(dictConfigUrl, "[\"phone.new.user.register\",\"login.account.activate\"]"),
            Post(new Uri(SsoBaseUri, "linkid/protected/api/aggregate/securitystrategy/passwordcontrol/config/get/byscene").ToString(), "{\"scene\":\"password.recover.config\"}"),
            Get(new Uri(SsoBaseUri, $"linkid/protected/api/aggregate/authmethod/usernames/UsernamePassword?{ts}").ToString()),
            Get(new Uri(SsoBaseUri, $"linkid/protected/api/aggregate/securitystrategy/passwordsecurity/passwordinitial/get?{ts}").ToString()),
        ];
    }

    private static Uri AddQuery(Uri uri, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(uri);
        var existing = builder.Query.TrimStart('?');
        var additions = string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        builder.Query = string.IsNullOrWhiteSpace(existing) ? additions : $"{existing}&{additions}";
        return builder.Uri;
    }

    private static Uri AbsoluteUri(Uri current, string location)
    {
        return Uri.TryCreate(location, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(current, location);
    }

    private static string ExtractTagValue(string html, string tagId)
    {
        var match = Regex.Match(html ?? string.Empty, $@"<p[^>]*id\s*=\s*[""']{Regex.Escape(tagId)}[""'][^>]*>(?<value>[^<]*)</p>", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value).Trim() : string.Empty;
    }

    private static string MatchAttribute(string attrs, string name)
    {
        var match = Regex.Match(attrs ?? string.Empty, $@"\b{Regex.Escape(name)}\s*=\s*[""'](?<value>[^""']*)[""']", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value).Trim() : string.Empty;
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html ?? string.Empty, "<.*?>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(text).Trim();
    }

    private static StudentProfileDocument ParseStudentProfile(string html, string fallbackUser)
        => new JwStudentProfileParser().Parse(html, fallbackUser);

    private static bool LooksLikeLoginPage(string html) => JwHtml.IsSessionExpired(html);

    private sealed record TraditionalPreloginRequest(
        string Method,
        string Url,
        Dictionary<string, string> Headers,
        string Body);

    private sealed record TraditionalJwFetchedSchedule(
        TraditionalJwScheduleParams Params,
        string Html);
}

public sealed record TraditionalJwLoginPageContext(
    string LoginUrl,
    string Execution,
    string CryptoBase64,
    string CaptchaUrl,
    string RedirectUri);

public sealed record TraditionalJwScheduleParams(
    string Rq,
    string SjmsValue,
    string TermLabel,
    string WeekLabel,
    string CurrentWeekLabel,
    IReadOnlyList<Dictionary<string, object?>> AvailableWeeks,
    IReadOnlyList<Dictionary<string, object?>> AvailableModes);

public sealed record TraditionalJwCaptchaChallenge(
    string CaptchaImage,
    int ExpiresInSeconds);

public sealed record TraditionalJwOptions
{
    public string DataRootPath { get; init; } = string.Empty;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public int ChallengeExpiresInSeconds { get; init; } = 120;

    public static TraditionalJwOptions CreateDefault()
    {
        return new TraditionalJwOptions
        {
            DataRootPath = Environment.GetEnvironmentVariable("XKE_DOTNET_DATA_DIR") ?? string.Empty,
        };
    }

    public static TraditionalJwOptions FromDataRoot(XkeDataRoot dataRoot)
    {
        return CreateDefault() with
        {
            DataRootPath = dataRoot.RootPath,
        };
    }
}
