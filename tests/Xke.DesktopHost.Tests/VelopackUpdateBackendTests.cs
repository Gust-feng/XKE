using System.IO;
using System.Security.Cryptography;
using System.Text;
using Velopack.NuGet;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;
using Xke.DesktopHost.Updates;
using Xunit;

namespace Xke.DesktopHost.Tests;

public sealed class VelopackUpdateBackendTests
{
    [Fact]
    public void UnpackagedBuildCanStillStartWithoutAnUpdaterInstallation()
    {
        VelopackApp.Build().SetArgs(Array.Empty<string>()).SetAutoApplyOnStartup(false).Run();
        var backend = new VelopackUpdateBackend();
        Assert.False(backend.IsInstalled); Assert.Null(backend.CurrentVersion); Assert.Null(backend.PendingVersion);
        using var service = new Xke.Core.Releases.AppUpdateService(backend, TimeProvider.System);
        Assert.Equal("unsupported", service.State.Status); Assert.Equal("1.0.0", service.State.CurrentVersion);
    }

    [Theory]
    [InlineData("../XKE-1.1.0-full.nupkg")]
    [InlineData("..\\XKE-1.1.0-full.nupkg")]
    [InlineData("C:alternate-full.nupkg")]
    [InlineData("XKE-1.1.0-full.nupkg:stream")]
    [InlineData("XKE-1.1.0-delta.nupkg")]
    public void InvalidPackagePathsAreRejectedBeforeDownloading(string filename)
        => Assert.Throws<InvalidDataException>(() => VelopackUpdateBackend.ValidateAsset(Asset(filename: filename)));

    [Theory]
    [InlineData("other-app", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3)]
    [InlineData("XKE", "sha1-only", 3)]
    [InlineData("XKE", "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg", 3)]
    [InlineData("XKE", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0)]
    public void IdentityStrongHashAndNonemptySizeAreRequired(string id, string hash, long size)
        => Assert.Throws<InvalidDataException>(() => VelopackUpdateBackend.ValidateAsset(Asset(id: id, hash: hash, size: size)));

    [Fact]
    public async Task ActualSdkRejectsDamagedDownloadBeforeItBecomesPending()
    {
        using var f = new Fixture(); f.Source.Content = Encoding.UTF8.GetBytes("damaged");
        Assert.Equal("1.1.0", await f.Backend.CheckAsync(default));
        await Assert.ThrowsAsync<ChecksumFailedException>(() => f.Backend.DownloadAsync(_ => { }, default));
        Assert.Null(f.Backend.PendingVersion);
        Assert.False(File.Exists(Path.Combine(f.Directory, f.Source.Asset.FileName)));
    }

    [Fact]
    public async Task ActualSdkRepairsDamagedCompleteCacheInsteadOfSkippingVerification()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Directory, f.Source.Asset.FileName), "damaged-cache");
        await f.Backend.CheckAsync(default);
        await f.Backend.DownloadAsync(_ => { }, default);
        Assert.Equal(1, f.Source.Downloads);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(f.Source.Content)), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(f.Directory, f.Source.Asset.FileName)))));
    }

    [Fact]
    public async Task ActualSdkDoesNotOfferDowngradeOrPrereleaseOnStableChannel()
    {
        using var f = new Fixture();
        f.Source.Asset.Version = SemanticVersion.Parse("0.9.0"); Assert.Null(await f.Backend.CheckAsync(default));
        f.Source.Asset.Version = SemanticVersion.Parse("1.1.0-beta.1");
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Backend.CheckAsync(default));
    }

    private static VelopackAsset Asset(string filename = "XKE-1.1.0-full.nupkg", string id = "XKE", string? hash = null, long size = 4)
        => new() { PackageId = id, Version = SemanticVersion.Parse("1.1.0"), Type = VelopackAssetType.Full,
            FileName = filename, SHA256 = hash ?? new string('A', 64), Size = size };

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "xke-update-sdk-" + Guid.NewGuid().ToString("N"));
        public Source Source { get; } = new();
        public VelopackUpdateBackend Backend { get; }
        public Fixture()
        {
            System.IO.Directory.CreateDirectory(Directory);
            var locator = new TestVelopackLocator("XKE", "1.0.0", Directory, appDir: Directory, rootDir: Directory,
                updateExe: Path.Combine(Directory, "never-started-update.exe"), channel: "win");
            Backend = new(new VerifiedUpdateManager(Source, new UpdateOptions { ExplicitChannel = "win", AllowVersionDowngrade = false }, locator));
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Directory);
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                || !Path.GetFileName(path).StartsWith("xke-update-sdk-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test cleanup");
            if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, true);
        }
    }
    private sealed class Source : IUpdateSource
    {
        public byte[] Content = Encoding.UTF8.GetBytes("synthetic-package-only");
        public VelopackAsset Asset { get; }
        public int Downloads;
        public Source() { Asset = VelopackUpdateBackendTests.Asset(hash: Convert.ToHexString(SHA256.HashData(Content)), size: Content.Length); }
        public Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
            => Task.FromResult(new VelopackAssetFeed { Assets = [Asset] });
        public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset asset, string file, Action<int> progress, CancellationToken cancelToken = default)
        { cancelToken.ThrowIfCancellationRequested(); Downloads++; File.WriteAllBytes(file, Content); progress(100); return Task.CompletedTask; }
    }
}
