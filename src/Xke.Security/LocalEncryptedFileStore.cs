using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Xke.Security;

public sealed class LocalEncryptedFileStore
{
    public const string EnvelopePrefix = "XKEDPAPI1.";
    public const string KeyId = "client://xke-dpapi-current-user-v1";
    public const string Algorithm = "DPAPI-CURRENT-USER";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string KeyFingerprint { get; } =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(KeyId))).ToLowerInvariant()[..16];

    public T? ReadJson<T>(string filePath, string? purpose = null)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        var plaintext = ReadBytes(filePath, purpose);
        return JsonSerializer.Deserialize<T>(plaintext, JsonOptions);
    }

    public void WriteJson<T>(string filePath, T value, string? purpose = null)
    {
        WriteBytes(filePath, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), purpose);
    }

    public string ReadText(string filePath, string? purpose = null)
    {
        return Encoding.UTF8.GetString(ReadBytes(filePath, purpose));
    }

    public void WriteText(string filePath, string value, string? purpose = null)
    {
        WriteBytes(filePath, Encoding.UTF8.GetBytes(value ?? string.Empty), purpose);
    }

    public byte[] ReadBytes(string filePath, string? purpose = null)
    {
        var encoded = File.ReadAllText(filePath, Encoding.UTF8).Trim().TrimStart('\uFEFF');
        if (!encoded.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Input is not an XKE local encrypted file.");
        }

        var protectedBytes = Convert.FromBase64String(encoded[EnvelopePrefix.Length..]);
        return XkeDpapi.Unprotect(protectedBytes, BuildPurpose(filePath, purpose));
    }

    public void WriteBytes(string filePath, byte[] content, string? purpose = null)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plaintext = content ?? [];
        var protectedBytes = XkeDpapi.Protect(plaintext, BuildPurpose(filePath, purpose));
        // DPAPI boundary: this store protects local runtime JSON/text using the
        // current Windows user profile and does not keep any repository key.
        File.WriteAllText(filePath, EnvelopePrefix + Convert.ToBase64String(protectedBytes), Encoding.UTF8);
    }

    private static string BuildPurpose(string filePath, string? purpose)
    {
        var normalizedPurpose = string.IsNullOrWhiteSpace(purpose)
            ? Path.GetFullPath(filePath).Replace('\\', '/')
            : purpose.Trim();
        return $"{EnvelopePrefix}|{KeyId}|{normalizedPurpose}";
    }
}
