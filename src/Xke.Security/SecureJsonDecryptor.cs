// 本文件对应 Python `core/remote_sensing/crypto.py` 中的 secure-json 解密语义。
// 职责：基于 telemetry AES key 对 legacy secure-json payload 做真实解密，
// 并校验 purpose、AAD 与 plaintext_sha256，供 Storage 兼容读取层复用。
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Xke.Security;

public sealed class SecureJsonDecryptor
{
    public JsonElement DecryptJsonElement(string envelopeText, string dataRootPath, string expectedPurpose = "")
    {
        var plaintext = DecryptToUtf8(envelopeText, dataRootPath, expectedPurpose);
        using var document = JsonDocument.Parse(plaintext);
        return document.RootElement.Clone();
    }

    public string DecryptToText(string envelopeText, string dataRootPath, string expectedPurpose = "")
    {
        var element = DecryptJsonElement(envelopeText, dataRootPath, expectedPurpose);
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Object when element.TryGetProperty("text", out var textElement) => textElement.GetString() ?? string.Empty,
            _ => element.GetRawText(),
        };
    }

    private static string DecryptToUtf8(string envelopeText, string dataRootPath, string expectedPurpose)
    {
        if (!SecureJsonEnvelope.TryParse(envelopeText, out var envelope) || envelope is null)
        {
            throw new InvalidDataException("Input is not a valid secure-json envelope.");
        }

        if (!string.IsNullOrWhiteSpace(expectedPurpose)
            && !string.IsNullOrWhiteSpace(envelope.Purpose)
            && !string.Equals(envelope.Purpose, expectedPurpose.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidDataException("secure json purpose mismatch");
        }

        var keyProfile = TelemetryKeyProfile.LoadFromDataRoot(dataRootPath);
        var fingerprint = string.IsNullOrWhiteSpace(envelope.KeyFingerprint)
            ? keyProfile.Fingerprint
            : envelope.KeyFingerprint;

        var nonce = Convert.FromBase64String(envelope.NonceB64);
        var ciphertext = Convert.FromBase64String(envelope.CiphertextB64);
        var tag = Convert.FromBase64String(envelope.TagB64);
        var plaintext = new byte[ciphertext.Length];
        var aad = Encoding.UTF8.GetBytes($"{SecureJsonEnvelope.FormatName}|{(envelope.Purpose ?? string.Empty).Trim()[..Math.Min((envelope.Purpose ?? string.Empty).Trim().Length, 64)]}|{fingerprint}");

        using var aesGcm = new AesGcm(keyProfile.KeyBytes, 16);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);

        if (!string.IsNullOrWhiteSpace(envelope.PlaintextSha256))
        {
            var digest = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
            if (!string.Equals(digest, envelope.PlaintextSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("secure json checksum mismatch");
            }
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
