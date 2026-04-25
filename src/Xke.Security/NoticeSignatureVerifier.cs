using System.Security.Cryptography;

namespace Xke.Security;

public sealed record NoticeSignatureVerificationResult(bool Valid, string Error);

public static class NoticeSignatureVerifier
{
    public static bool VerifyIndexSignature(object? payload, string signatureText, string publicKeyPem)
    {
        return VerifyIndexSignatureDetailed(payload, signatureText, publicKeyPem).Valid;
    }

    public static NoticeSignatureVerificationResult VerifyIndexSignatureDetailed(object? payload, string signatureText, string publicKeyPem)
    {
        var signatureInput = (signatureText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(signatureInput))
        {
            return new NoticeSignatureVerificationResult(false, "公告索引签名缺失");
        }

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return new NoticeSignatureVerificationResult(false, "公告验签公钥缺失");
        }

        byte[] signature;
        try
        {
            var normalizedSignature = signatureInput
                .Replace('-', '+')
                .Replace('_', '/');
            var padding = (4 - normalizedSignature.Length % 4) % 4;
            signature = Convert.FromBase64String(normalizedSignature + new string('=', padding));
        }
        catch (FormatException)
        {
            return new NoticeSignatureVerificationResult(false, "公告索引签名格式不合法");
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);
            var canonicalBytes = NoticeCanonicalJson.Serialize(payload);
            return ecdsa.VerifyData(canonicalBytes, signature, HashAlgorithmName.SHA512)
                ? new NoticeSignatureVerificationResult(true, string.Empty)
                : new NoticeSignatureVerificationResult(false, "公告索引签名校验失败");
        }
        catch (CryptographicException)
        {
            return new NoticeSignatureVerificationResult(false, "公告验签公钥不是 ECDSA 公钥或 PEM 格式不受支持");
        }
        catch
        {
            return new NoticeSignatureVerificationResult(false, "公告索引签名校验异常");
        }
    }
}
