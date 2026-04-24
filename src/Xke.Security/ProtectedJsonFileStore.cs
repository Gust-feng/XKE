using System.Text;
using System.Text.Json;

namespace Xke.Security;

public sealed class ProtectedJsonFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public T? Read<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        var encoded = File.ReadAllText(filePath, Encoding.UTF8).Trim();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return default;
        }

        var protectedBytes = Convert.FromBase64String(encoded);
        var plaintextBytes = XkeDpapi.Unprotect(protectedBytes);
        return JsonSerializer.Deserialize<T>(plaintextBytes, JsonOptions);
    }

    public void Write<T>(string filePath, T value)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var protectedBytes = XkeDpapi.Protect(plaintextBytes);
        // DPAPI boundary: callers decide the .NET data-root path; this store only
        // persists protected JSON bytes and never writes plaintext secrets.
        File.WriteAllText(filePath, Convert.ToBase64String(protectedBytes), Encoding.UTF8);
    }
}
