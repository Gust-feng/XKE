using System.Security.Cryptography;
using System.Text;

namespace Xke.Storage;

public sealed class LegacyNoticeCacheRoot
{
    public const string CacheVersion = "v1";
    public const string CacheSalt = "xke-notice-cache|2026-03-21";

    public string DirectoryPath { get; }
    public string IndexPath => CacheKeyToPath("index");
    public string IndexSignaturePath => CacheKeyToPath("index-signature");

    public LegacyNoticeCacheRoot(string directoryPath)
    {
        DirectoryPath = directoryPath;
    }

    public string CacheKeyToPath(string cacheKey)
    {
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{CacheSalt}|{CacheVersion}|{cacheKey}"))
        ).ToLowerInvariant();
        return Path.Combine(DirectoryPath, digest[..2], digest.Substring(2, 2), $"{digest}.bin");
    }

    public string CacheKeyForRelativePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim().TrimStart('/');
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : $"asset:{normalized}";
    }

    public string? PayloadPath(string relativePath)
    {
        var cacheKey = CacheKeyForRelativePath(relativePath);
        return string.IsNullOrWhiteSpace(cacheKey) ? null : CacheKeyToPath(cacheKey);
    }
}
