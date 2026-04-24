using System.Text.Json;

namespace Xke.Storage;

public static class LegacySecureJsonDetector
{
    public const string SecureJsonFormat = "xke-secure-json";
    public const string SecureJsonAlgorithm = "AES-256-GCM";

    public static bool IsSecureJsonEnvelope(string text)
    {
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

            var format = root.TryGetProperty("format", out var formatElement)
                ? formatElement.GetString()
                : null;
            var algorithm = root.TryGetProperty("algorithm", out var algorithmElement)
                ? algorithmElement.GetString()
                : null;
            var hasCiphertext = root.TryGetProperty("ciphertext_b64", out var ciphertextElement) && !string.IsNullOrWhiteSpace(ciphertextElement.GetString());
            var hasNonce = root.TryGetProperty("nonce_b64", out var nonceElement) && !string.IsNullOrWhiteSpace(nonceElement.GetString());
            var hasTag = root.TryGetProperty("tag_b64", out var tagElement) && !string.IsNullOrWhiteSpace(tagElement.GetString());

            return string.Equals(format, SecureJsonFormat, StringComparison.Ordinal)
                && string.Equals(algorithm, SecureJsonAlgorithm, StringComparison.Ordinal)
                && hasCiphertext
                && hasNonce
                && hasTag;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
