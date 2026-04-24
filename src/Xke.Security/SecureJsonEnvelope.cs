using System.Text.Json;

namespace Xke.Security;

public sealed class SecureJsonEnvelope
{
    public const string FormatName = "xke-secure-json";
    public const string AlgorithmName = "AES-256-GCM";

    public string Format { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public string KeyFingerprint { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string PlaintextSha256 { get; init; } = string.Empty;
    public string NonceB64 { get; init; } = string.Empty;
    public string CiphertextB64 { get; init; } = string.Empty;
    public string TagB64 { get; init; } = string.Empty;

    public static bool TryParse(string text, out SecureJsonEnvelope? envelope)
    {
        envelope = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var format = root.TryGetProperty("format", out var formatElement) ? (formatElement.GetString() ?? string.Empty).Trim() : string.Empty;
            var algorithm = root.TryGetProperty("algorithm", out var algorithmElement) ? (algorithmElement.GetString() ?? string.Empty).Trim() : string.Empty;
            var ciphertext = root.TryGetProperty("ciphertext_b64", out var ciphertextElement) ? (ciphertextElement.GetString() ?? string.Empty).Trim() : string.Empty;
            var nonce = root.TryGetProperty("nonce_b64", out var nonceElement) ? (nonceElement.GetString() ?? string.Empty).Trim() : string.Empty;
            var tag = root.TryGetProperty("tag_b64", out var tagElement) ? (tagElement.GetString() ?? string.Empty).Trim() : string.Empty;

            if (!string.Equals(format, FormatName, StringComparison.Ordinal)
                || !string.Equals(algorithm, AlgorithmName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(ciphertext)
                || string.IsNullOrWhiteSpace(nonce)
                || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            envelope = new SecureJsonEnvelope
            {
                Format = format,
                Algorithm = algorithm,
                KeyFingerprint = root.TryGetProperty("key_fingerprint", out var keyFingerprintElement) ? (keyFingerprintElement.GetString() ?? string.Empty).Trim() : string.Empty,
                Purpose = root.TryGetProperty("purpose", out var purposeElement) ? (purposeElement.GetString() ?? string.Empty).Trim() : string.Empty,
                PlaintextSha256 = root.TryGetProperty("plaintext_sha256", out var digestElement) ? (digestElement.GetString() ?? string.Empty).Trim() : string.Empty,
                NonceB64 = nonce,
                CiphertextB64 = ciphertext,
                TagB64 = tag,
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
