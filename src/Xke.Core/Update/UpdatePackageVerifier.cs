using System.Security.Cryptography;

namespace Xke.Core.Update;

public static class UpdatePackageVerifier
{
    public static async Task<(bool Ok, string Error, string Sha256, long Size)> VerifyAsync(
        string packagePath,
        string expectedSha256,
        long expectedSize,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            return (false, "更新包不存在", string.Empty, 0);
        }

        var info = new FileInfo(packagePath);
        if (expectedSize > 0 && info.Length != expectedSize)
        {
            return (false, $"更新包大小不匹配：期望 {expectedSize}，实际 {info.Length}", string.Empty, info.Length);
        }

        await using var stream = File.OpenRead(packagePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(sha256, (expectedSha256 ?? string.Empty).Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return (false, "更新包 SHA256 校验失败", sha256, info.Length);
        }

        return (true, string.Empty, sha256, info.Length);
    }
}
