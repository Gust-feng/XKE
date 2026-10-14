using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace Xke.DesktopHost.Updates;

internal sealed class VerifiedUpdateManager(IUpdateSource source, UpdateOptions? options = null, IVelopackLocator? locator = null)
    : UpdateManager(source, options, locator)
{
    public Task VerifyDownloadedAsync(VelopackAsset asset) => VerifyPackageChecksumAsync(asset);

    public async Task RepairDamagedCacheAsync(VelopackAsset asset)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Locator.PackagesDir!, asset.FileName));
        var root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(Locator.PackagesDir!)) + System.IO.Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new System.IO.InvalidDataException("Invalid update cache path");
        if (!System.IO.File.Exists(path)) return;
        try { await VerifyPackageChecksumAsync(asset).ConfigureAwait(false); }
        catch (ChecksumFailedException) { System.IO.File.Delete(path); }
    }
}
