using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Xke.WebHost;

/// <summary>
/// Access boundary for the loopback WebHost, not the independently hosted ProtocolHost.
/// Call before CORS so rejected API requests cannot be short-circuited by preflight handling.
/// </summary>
public static class LocalApiAccessPolicy
{
    public const string DesktopTokenHeaderName = "X-XKE-Desktop";

    /// <summary>
    /// Validates Host for every request, explicit Origin for API requests, and desktop
    /// proof for sensitive desktop operations. Missing Origin is allowed for native clients.
    /// Returns false with status 403; the caller must stop the pipeline in that case.
    /// Allowed frontend origins are explicit configuration, never an implicit loopback wildcard.
    /// </summary>
    public static bool ValidateRequest(
        HttpContext context,
        string? hostMode,
        IReadOnlyList<string> allowedFrontendOrigins,
        string? desktopRequestToken)
    {
        var request = context.Request;
        if (!TryGetLocalOrigin(request, out _))
            return Forbid(context);

        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return true;

        if (request.Headers.TryGetValue("Origin", out var origins)
            && (origins.Count != 1
                || !IsAllowedOrigin(request, origins[0] ?? string.Empty, allowedFrontendOrigins)))
        {
            return Forbid(context);
        }

        // Browsers cannot attach the token itself to a CORS preflight. CORS still
        // controls allowed methods/headers; the actual operation requires proof below.
        if (HttpMethods.IsOptions(request.Method)
            || !string.Equals(hostMode?.Trim(), "desktop-webview", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Routing accepts a trailing slash and ignores path casing; match that boundary.
        var path = request.Path.Value?.TrimEnd('/');
        var requiresDesktopToken =
            string.Equals(path, "/api/desktop/auth-bootstrap", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/api/desktop/settings", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/api/app/updates", StringComparison.OrdinalIgnoreCase);

        if (!requiresDesktopToken)
            return true;

        if (string.IsNullOrWhiteSpace(desktopRequestToken)
            || !request.Headers.TryGetValue(DesktopTokenHeaderName, out var tokens)
            || tokens.Count != 1
            || string.IsNullOrEmpty(tokens[0]))
        {
            return Forbid(context);
        }

        // Hashes have fixed length, including when the supplied token has a different
        // length. Neither the configured token nor the received token is logged/stored.
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(desktopRequestToken));
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(tokens[0]!));
        return CryptographicOperations.FixedTimeEquals(expected, actual) || Forbid(context);
    }

    /// <summary>
    /// Compares HTTP(S) origins by scheme, host and effective port. Host aliases and
    /// different localhost ports are not interchangeable. Paths are not valid origins.
    /// The request authority must itself be loopback, even for an allowlisted frontend.
    /// </summary>
    public static bool IsAllowedOrigin(
        HttpRequest request,
        string origin,
        IReadOnlyList<string> allowedFrontendOrigins)
    {
        if (!TryGetLocalOrigin(request, out var localOrigin)
            || !TryParseOrigin(origin, out var candidate))
        {
            return false;
        }

        if (SameOrigin(localOrigin, candidate))
            return true;

        return allowedFrontendOrigins.Any(value =>
            TryParseOrigin(value, out var allowed) && SameOrigin(allowed, candidate));
    }

    private static bool TryGetLocalOrigin(HttpRequest request, out ParsedOrigin origin)
    {
        if (!TryParseOrigin($"{request.Scheme}://{request.Host.Value}", out origin))
            return false;

        // Do not resolve DNS or trust Forwarded/X-Forwarded-Host here. A hostname
        // resolving to loopback is not sufficient protection against DNS rebinding.
        var host = origin.Host.Trim('[', ']');
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    }

    private static bool TryParseOrigin(string value, out ParsedOrigin origin)
    {
        origin = default;
        if (string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace))
            return false;

        var schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
            return false;

        // Origin is an authority, not a URL: reject paths (including '/'), credentials,
        // query/fragment components, backslash normalization and joined header values.
        var authority = value.AsSpan(schemeEnd + 3);
        if (authority.IsEmpty || authority.IndexOfAny("/\\?#@,%".AsSpan()) >= 0)
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || !parsed.IsWellFormedOriginalString()
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || parsed.Host.Length == 0)
        {
            return false;
        }

        var hostLength = authority[0] == '['
            ? authority.IndexOf(']') + 1
            : authority.IndexOf(':');
        if (hostLength < 0)
            hostLength = authority.Length;
        if (hostLength == 0 || authority[^1] == ':')
            return false;

        // Uri canonicalizes shorthand IPv4 and alternate IPv6 spellings. Preserve
        // the supplied host so those aliases do not broaden exact-origin trust.
        origin = new ParsedOrigin(parsed.Scheme, authority[..hostLength].ToString(), parsed.Port);
        return true;
    }

    private static bool SameOrigin(ParsedOrigin left, ParsedOrigin right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    private readonly record struct ParsedOrigin(string Scheme, string Host, int Port);

    private static bool Forbid(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return false;
    }
}
