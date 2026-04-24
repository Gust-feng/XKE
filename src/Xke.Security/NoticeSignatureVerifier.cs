using System.Security.Cryptography;

namespace Xke.Security;

public static class NoticeSignatureVerifier
{
    public static bool VerifyIndexSignature(object? payload, string signatureText, string publicKeyPem)
    {
        var signatureInput = (signatureText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(signatureInput) || string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return false;
        }

        try
        {
            var padding = (4 - signatureInput.Length % 4) % 4;
            var signature = Convert.FromBase64String(signatureInput + new string('=', padding));
            var canonicalBytes = NoticeCanonicalJson.Serialize(payload);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);
            return ecdsa.VerifyData(canonicalBytes, signature, HashAlgorithmName.SHA512);
        }
        catch
        {
            return false;
        }
    }
}
