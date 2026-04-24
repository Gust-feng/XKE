using System.Text;
using System.Text.Json;
using Xke.Security;

namespace Xke.Storage;

public sealed class LegacyNoticeCacheReader
{
    private readonly LegacyNoticeCacheRoot _cacheRoot;

    public LegacyNoticeCacheReader(LegacyNoticeCacheRoot cacheRoot)
    {
        _cacheRoot = cacheRoot;
    }

    public LegacyReadResult<JsonElement> ReadProtectedJson(string path)
    {
        if (!File.Exists(path))
        {
            return new LegacyReadResult<JsonElement> { Exists = false, SourcePath = path };
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var rawBytes = NoticeDpapi.Unprotect(protectedBytes);
            using var document = JsonDocument.Parse(rawBytes);
            return new LegacyReadResult<JsonElement>
            {
                Exists = true,
                SourcePath = path,
                HasValue = true,
                Value = document.RootElement.Clone(),
            };
        }
        catch
        {
            return new LegacyReadResult<JsonElement>
            {
                Exists = true,
                SourcePath = path,
            };
        }
    }

    public LegacyReadResult<string> ReadProtectedText(string path)
    {
        if (!File.Exists(path))
        {
            return new LegacyReadResult<string> { Exists = false, SourcePath = path };
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var rawBytes = NoticeDpapi.Unprotect(protectedBytes);
            return new LegacyReadResult<string>
            {
                Exists = true,
                SourcePath = path,
                HasValue = true,
                Value = DecodeText(rawBytes),
            };
        }
        catch
        {
            return new LegacyReadResult<string>
            {
                Exists = true,
                SourcePath = path,
            };
        }
    }

    public LegacyReadResult<JsonElement> ReadIndex()
    {
        return ReadProtectedJson(_cacheRoot.IndexPath);
    }

    public LegacyReadResult<string> ReadIndexSignature()
    {
        return ReadProtectedText(_cacheRoot.IndexSignaturePath);
    }

    public LegacyNoticeValidationResult<JsonElement> ReadAndValidateIndex(string appRoot, string? publicKeyPem = null)
    {
        var index = ReadIndex();
        var signature = ReadIndexSignature();
        if (!index.Exists || !index.HasValue)
        {
            return new LegacyNoticeValidationResult<JsonElement>
            {
                Exists = false,
                SourcePath = index.SourcePath,
                Error = "notice index missing",
            };
        }

        if (!signature.Exists || !signature.HasValue || string.IsNullOrWhiteSpace(signature.Value))
        {
            return new LegacyNoticeValidationResult<JsonElement>
            {
                Exists = true,
                SourcePath = index.SourcePath,
                HasValue = index.HasValue,
                Value = index.Value,
                Error = "notice index signature missing",
            };
        }

        string resolvedPublicKeyPem;
        try
        {
            resolvedPublicKeyPem = string.IsNullOrWhiteSpace(publicKeyPem)
                ? NoticePublicKeyLoader.LoadPem(appRoot)
                : publicKeyPem;
        }
        catch (Exception ex)
        {
            return new LegacyNoticeValidationResult<JsonElement>
            {
                Exists = true,
                SourcePath = index.SourcePath,
                HasValue = index.HasValue,
                Value = index.Value,
                Error = $"notice public key load failed: {ex.Message}",
            };
        }

        var signatureValid = NoticeSignatureVerifier.VerifyIndexSignature(index.Value, signature.Value, resolvedPublicKeyPem);
        return new LegacyNoticeValidationResult<JsonElement>
        {
            Exists = true,
            SourcePath = index.SourcePath,
            HasValue = index.HasValue,
            Value = index.Value,
            SignatureValid = signatureValid,
            HashValid = false,
            Error = signatureValid ? string.Empty : "notice index signature invalid",
        };
    }

    public LegacyNoticeValidationResult<JsonElement> ReadPayloadJsonValidated(string relativePath, string expectedSha256)
    {
        var payload = ReadPayloadJson(relativePath);
        if (!payload.Exists || !payload.HasValue)
        {
            return new LegacyNoticeValidationResult<JsonElement>
            {
                Exists = false,
                SourcePath = payload.SourcePath,
                Error = "payload missing",
            };
        }

        var canonicalBytes = NoticeCanonicalJson.Serialize(payload.Value);
        var actualSha256 = NoticeHash.Sha256Hex(canonicalBytes);
        var hashValid = string.Equals((expectedSha256 ?? string.Empty).Trim().ToLowerInvariant(), actualSha256, StringComparison.Ordinal);
        return new LegacyNoticeValidationResult<JsonElement>
        {
            Exists = true,
            SourcePath = payload.SourcePath,
            HasValue = payload.HasValue,
            Value = payload.Value,
            HashValid = hashValid,
            Error = hashValid ? string.Empty : $"payload sha256 mismatch: {actualSha256}",
        };
    }

    public LegacyReadResult<JsonElement> ReadPayloadJson(string relativePath)
    {
        var path = _cacheRoot.PayloadPath(relativePath);
        return string.IsNullOrWhiteSpace(path)
            ? new LegacyReadResult<JsonElement> { Exists = false, SourcePath = string.Empty }
            : ReadProtectedJson(path);
    }

    public LegacyReadResult<string> ReadPayloadText(string relativePath)
    {
        var path = _cacheRoot.PayloadPath(relativePath);
        return string.IsNullOrWhiteSpace(path)
            ? new LegacyReadResult<string> { Exists = false, SourcePath = string.Empty }
            : ReadProtectedText(path);
    }

    public void WriteIndex(JsonElement payload)
    {
        WriteProtectedBytes(_cacheRoot.IndexPath, JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void WriteIndexSignature(string signatureText)
    {
        WriteProtectedBytes(_cacheRoot.IndexSignaturePath, Encoding.UTF8.GetBytes(signatureText ?? string.Empty));
    }

    public void WritePayloadJson(string relativePath, JsonElement payload)
    {
        var path = _cacheRoot.PayloadPath(relativePath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        WriteProtectedBytes(path, JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void WritePayloadText(string relativePath, string text)
    {
        var path = _cacheRoot.PayloadPath(relativePath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        WriteProtectedBytes(path, NoticeCompatibility.NoticeTextBytes(text ?? string.Empty));
    }

    private static void WriteProtectedBytes(string path, byte[] rawBytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, NoticeDpapi.Protect(rawBytes));
    }

    private static string DecodeText(byte[] rawBytes)
    {
        foreach (var encodingName in new[] { "utf-8", "utf-8-sig" })
        {
            try
            {
                return Encoding.GetEncoding(encodingName).GetString(rawBytes);
            }
            catch
            {
            }
        }

        return Encoding.UTF8.GetString(rawBytes);
    }
}
