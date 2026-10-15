using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class LocalApiAccessPolicyTests
{
    private const string DesktopMode = "desktop-webview";
    private const string Token = "synthetic-desktop-proof-not-a-real-secret";
    private static readonly IReadOnlyList<string> NoOrigins = Array.Empty<string>();

    [Theory]
    [InlineData("http", "127.0.0.1:38443", "http://127.0.0.1:38443")]
    [InlineData("http", "localhost:38443", "http://localhost:38443")]
    [InlineData("http", "LOCALHOST:38443", "http://localhost:38443")]
    [InlineData("http", "[::1]:38443", "http://[::1]:38443")]
    [InlineData("http", "localhost", "http://localhost:80")]
    [InlineData("https", "localhost:443", "https://localhost")]
    public void IsAllowedOrigin_AcceptsSameSchemeHostAndEffectivePort(
        string scheme, string host, string origin)
    {
        var context = Request(host: host, scheme: scheme);
        Assert.True(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, origin, NoOrigins));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://localhost:38443")]
    [InlineData("https://127.0.0.1:38443")]
    [InlineData("http://[::1]:38443")]
    [InlineData("http://127.1:38443")]
    [InlineData("http://2130706433:38443")]
    [InlineData("https://untrusted.example")]
    public void IsAllowedOrigin_DoesNotTrustOtherPortsSchemesOrHostAliases(string origin)
    {
        var context = Request();
        Assert.False(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, origin, NoOrigins));
    }

    [Fact]
    public void IsAllowedOrigin_UsesOnlyExplicitDevelopmentAllowlist()
    {
        var context = Request();
        string[] allowed = ["http://localhost:5173", "https://frontend.example:8443"];
        Assert.True(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, allowed[0], allowed));
        Assert.True(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, allowed[1], allowed));
        Assert.False(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, "http://localhost:5174", allowed));
        Assert.False(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, "http://127.0.0.1:5173", allowed));
        Assert.False(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, "https://frontend.example", allowed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("*")]
    [InlineData("file://localhost")]
    [InlineData("//127.0.0.1:38443")]
    [InlineData("http://127.0.0.1:38443/")]
    [InlineData("http://127.0.0.1:38443/path")]
    [InlineData("http://127.0.0.1:38443?query=1")]
    [InlineData("http://127.0.0.1:38443#fragment")]
    [InlineData("http://user@127.0.0.1:38443")]
    [InlineData("http://127.0.0.1:38443\\path")]
    [InlineData(" http://127.0.0.1:38443")]
    [InlineData("http://127.0.0.1:38443 ")]
    [InlineData("http://127.0.0.1:38443,http://localhost:5173")]
    [InlineData("http://127.0.0.1:38443 http://localhost:5173")]
    [InlineData("http://127.0.0.1:65536")]
    [InlineData("http://127.0.0.1:")]
    [InlineData("http://%6cocalhost:38443")]
    public void IsAllowedOrigin_RejectsMalformedOrOpaqueOrigins_EvenIfAllowlisted(string origin)
    {
        var context = Request();
        Assert.False(LocalApiAccessPolicy.IsAllowedOrigin(context.Request, origin, [origin]));
    }

    [Theory]
    [InlineData("https://untrusted.example")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("null")]
    [InlineData("")]
    public void ValidateRequest_ExplicitDisallowedApiOriginReturns403(string origin)
    {
        var context = Request();
        context.Request.Headers.Origin = origin;
        AssertForbidden(context);
    }

    [Fact]
    public void ValidateRequest_RejectsMultipleOriginValues_EvenWhenBothAreTrusted()
    {
        var context = Request();
        context.Request.Headers.Origin = new StringValues(
            ["http://127.0.0.1:38443", "http://localhost:5173"]);
        AssertForbidden(context, allowed: ["http://localhost:5173"]);
    }

    [Fact]
    public void ValidateRequest_RejectsJoinedOriginValues()
    {
        var context = Request();
        context.Request.Headers.Origin = "http://127.0.0.1:38443, http://127.0.0.1:38443";
        AssertForbidden(context);
    }

    [Theory]
    [InlineData("attacker.example:38443")]
    [InlineData("localhost.attacker.example:38443")]
    [InlineData("localhost.:38443")]
    [InlineData("192.0.2.1:38443")]
    [InlineData("0.0.0.0:38443")]
    [InlineData("[::]:38443")]
    [InlineData("[2001:db8::1]:38443")]
    [InlineData("localhost:bad-port")]
    [InlineData("localhost:65536")]
    [InlineData("localhost:38443/path")]
    [InlineData("")]
    public void ValidateRequest_RejectsNonLoopbackOrMalformedHostWithoutOrigin(string host)
    {
        var context = Request(host: host);
        AssertForbidden(context);
        Assert.False(LocalApiAccessPolicy.IsAllowedOrigin(
            context.Request, "http://localhost:5173", ["http://localhost:5173"]));
    }

    [Fact]
    public void ValidateRequest_ForwardedLoopbackHeadersDoNotMakeRemoteHostTrusted()
    {
        var context = Request(host: "attacker.example:38443");
        context.Request.Headers["X-Forwarded-Host"] = "127.0.0.1:38443";
        context.Request.Headers["Forwarded"] = "host=127.0.0.1:38443;proto=http";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        AssertForbidden(context);
    }

    [Fact]
    public void ValidateRequest_ValidDesktopTokenCannotBypassOriginCheck()
    {
        var context = Request("/api/desktop/auth-bootstrap", HttpMethods.Post);
        context.Request.Headers.Origin = "http://localhost:5173";
        context.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = Token;
        AssertForbidden(context, configuredToken: Token);
    }

    [Theory]
    [InlineData("/api/health", "GET")]
    [InlineData("/health", "GET")]
    [InlineData("/api/auth/login", "POST")]
    [InlineData("/api/schedule/local", "GET")]
    [InlineData("/api/desktop/activate", "POST")]
    public void ValidateRequest_OriginlessOrdinaryRequestsDoNotRequireDesktopProof(string path, string method)
    {
        var context = Request(path, method);
        AssertAllowed(context);
    }

    [Theory]
    [InlineData("http://127.0.0.1:38443")]
    [InlineData("http://localhost:5173")]
    public void ValidateRequest_AllowsSameOriginOrConfiguredFrontendForOrdinaryApi(string origin)
    {
        var context = Request();
        context.Request.Headers.Origin = origin;
        AssertAllowed(context, allowed: ["http://localhost:5173"]);
    }

    [Theory]
    [InlineData("/api/desktop/auth-bootstrap")]
    [InlineData("/api/desktop/auth-bootstrap/")]
    [InlineData("/API/Desktop/Auth-Bootstrap")]
    [InlineData("/api/desktop/settings")]
    [InlineData("/API/Desktop/Settings/")]
    public void ValidateRequest_DesktopOperationsFailClosedWithoutConfiguredToken(string path)
    {
        var context = Request(path, HttpMethods.Post);
        context.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = Token;
        AssertForbidden(context);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public void ValidateRequest_DesktopSettingsReadAndWriteRequireProof(string method)
    {
        var missing = Request("/api/desktop/settings", method);
        AssertForbidden(missing, configuredToken: Token);

        var unconfigured = Request("/api/desktop/settings", method);
        unconfigured.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = Token;
        AssertForbidden(unconfigured);

        var incorrect = Request("/api/desktop/settings", method);
        incorrect.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = "incorrect-proof";
        AssertForbidden(incorrect, configuredToken: Token);

        var valid = Request("/api/desktop/settings", method);
        valid.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = Token;
        AssertAllowed(valid, configuredToken: Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ValidateRequest_EmptyConfiguredTokenIsNotProof(string? configuredToken)
    {
        var context = Request("/api/desktop/auth-bootstrap", HttpMethods.Post);
        context.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = configuredToken ?? Token;
        AssertForbidden(context, configuredToken: configuredToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-token")]
    [InlineData(Token + "x")]
    [InlineData(" " + Token)]
    [InlineData("SYNTHETIC-DESKTOP-PROOF-NOT-A-REAL-SECRET")]
    public void ValidateRequest_RejectsMissingOrIncorrectDesktopToken(string? suppliedToken)
    {
        var context = Request("/api/desktop/auth-bootstrap", HttpMethods.Post);
        if (suppliedToken is not null)
            context.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = suppliedToken;
        AssertForbidden(context, configuredToken: Token);
    }

    [Fact]
    public void ValidateRequest_RejectsMultipleDesktopTokens()
    {
        var context = Request("/api/desktop/settings", HttpMethods.Post);
        context.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = new StringValues([Token, Token]);
        AssertForbidden(context, configuredToken: Token);
    }

    [Theory]
    [InlineData("/api/desktop/auth-bootstrap")]
    [InlineData("/api/desktop/auth-bootstrap/")]
    [InlineData("/API/Desktop/Settings/")]
    public void ValidateRequest_AcceptsExactDesktopProofWithOrWithoutOrigin(string path)
    {
        foreach (var origin in new string?[] { null, "http://127.0.0.1:38443", "http://localhost:5173" })
        {
            var context = Request(path, HttpMethods.Post);
            if (origin is not null)
                context.Request.Headers.Origin = origin;
            context.Request.Headers[LocalApiAccessPolicy.DesktopTokenHeaderName] = Token;
            AssertAllowed(context, configuredToken: Token, allowed: ["http://localhost:5173"]);
        }
    }

    [Theory]
    [InlineData("standalone")]
    [InlineData("other-http-host")]
    [InlineData(null)]
    public void ValidateRequest_NonDesktopHostsLeaveBootstrap404AndNoOpSettingsToEndpoints(string? hostMode)
    {
        foreach (var (path, method) in new[]
        {
            ("/api/desktop/auth-bootstrap", HttpMethods.Post),
            ("/api/desktop/settings", HttpMethods.Get),
            ("/api/desktop/settings", HttpMethods.Post),
        })
        {
            var context = Request(path, method);
            Assert.True(LocalApiAccessPolicy.ValidateRequest(context, hostMode, NoOrigins, null));
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/desktop/auth-bootstrap")]
    [InlineData("/api/desktop/settings")]
    public void ValidateRequest_PreflightAllowsTrustedOriginWithoutToken_ButRejectsForeignOrigin(string path)
    {
        var context = Request(path, HttpMethods.Options);
        context.Request.Headers.Origin = "http://localhost:5173";
        context.Request.Headers["Access-Control-Request-Method"] = "POST";
        context.Request.Headers["Access-Control-Request-Headers"] = LocalApiAccessPolicy.DesktopTokenHeaderName;
        AssertAllowed(context, allowed: ["http://localhost:5173"]);

        context.Request.Headers.Origin = "http://untrusted.example";
        AssertForbidden(context, allowed: ["http://localhost:5173"]);
    }

    [Fact]
    public void ValidateRequest_ChecksApiSegmentNotArbitraryPrefix()
    {
        var context = Request("/apiary");
        context.Request.Headers.Origin = "null";
        AssertAllowed(context);
    }

    [Fact]
    public void ValidateRequest_AlsoRejectsRebindingHostForStaticContent()
    {
        var context = Request("/index.html", host: "attacker.example:38443");
        AssertForbidden(context);
    }

    private static DefaultHttpContext Request(
        string path = "/api/health",
        string method = "GET",
        string host = "127.0.0.1:38443",
        string scheme = "http")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Request.Method = method;
        return context;
    }

    private static void AssertAllowed(
        HttpContext context, string? configuredToken = null, IReadOnlyList<string>? allowed = null)
    {
        Assert.True(LocalApiAccessPolicy.ValidateRequest(context, DesktopMode, allowed ?? NoOrigins, configuredToken));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static void AssertForbidden(
        HttpContext context, string? configuredToken = null, IReadOnlyList<string>? allowed = null)
    {
        Assert.False(LocalApiAccessPolicy.ValidateRequest(context, DesktopMode, allowed ?? NoOrigins, configuredToken));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
