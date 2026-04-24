using System.Security.Cryptography;

namespace Xke.Security;

public static class NoticeHash
{
    public static string Sha256Hex(byte[] rawBytes)
    {
        return Convert.ToHexString(SHA256.HashData(rawBytes ?? [])).ToLowerInvariant();
    }
}
