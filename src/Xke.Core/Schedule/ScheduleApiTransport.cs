using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Xke.Core.Schedule;

public static class ScheduleApiTransport
{
    public static HttpClientHandler CreateHandler(ScheduleApiOptions options)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        if (!string.IsNullOrWhiteSpace(options.CertificateSha256))
        {
            var fingerprint = NormalizeFingerprint(options.CertificateSha256);
            handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                IsTrustedCertificate(request, certificate, errors, fingerprint);
        }
        return handler;
    }

    public static bool IsValidFingerprint(string value)
    {
        var normalized = NormalizeFingerprint(value);
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit);
    }

    private static string NormalizeFingerprint(string value) => value.Replace(":", string.Empty).Trim();

    private static bool IsTrustedCertificate(HttpRequestMessage request, X509Certificate2? certificate,
        SslPolicyErrors errors, string fingerprint)
    {
        if (!IsValidFingerprint(fingerprint) || certificate is null || request.RequestUri?.Scheme != "https"
            || (errors & (SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)) != 0)
            return false;
        var now = DateTime.UtcNow;
        if (certificate.NotBefore.ToUniversalTime() > now || certificate.NotAfter.ToUniversalTime() <= now) return false;
        // Exact private certificate trust permits its self-signed chain, never a blanket TLS bypass.
        return string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), fingerprint, StringComparison.OrdinalIgnoreCase);
    }
}
