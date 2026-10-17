using System.IO;
using System.Net.Http;
using Velopack;
using Velopack.Sources;
using Xke.Core.Releases;

namespace Xke.DesktopHost.Updates;

internal sealed class VelopackUpdateBackend : IAppUpdateBackend
{
    private readonly VerifiedUpdateManager _manager;
    private UpdateInfo? _available;

    public VelopackUpdateBackend(VerifiedUpdateManager? manager = null)
        => _manager = manager ?? new VerifiedUpdateManager(new GithubSource(AppRelease.RepositoryUrl, null, false, new UpdateDownloader()),
            new UpdateOptions { AllowVersionDowngrade = false, ExplicitChannel = "win" });

    public bool IsInstalled => _manager.IsInstalled && _manager.AppId == AppRelease.PackageId;
    public string? CurrentVersion => IsInstalled ? _manager.CurrentVersion?.ToNormalizedString() : null;
    public string? PendingVersion => IsInstalled ? _manager.UpdatePendingRestart?.Version.ToNormalizedString() : null;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        var update = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (update is not null)
        {
            ValidateAsset(update.TargetFullRelease);
            if (update.IsDowngrade || update.TargetFullRelease.Version.IsPrerelease) throw new InvalidDataException("Only stable forward updates are supported");
        }
        _available = update;
        return update?.TargetFullRelease.Version.ToNormalizedString();
    }

    public async Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken)
    {
        var update = _available ?? throw new InvalidOperationException("Check for updates before downloading");
        ValidateAsset(update.TargetFullRelease);
        await _manager.RepairDamagedCacheAsync(update.TargetFullRelease).ConfigureAwait(false);
        // The SDK checks size and SHA256 before it makes a full package available for installation.
        await _manager.DownloadUpdatesAsync(update, progress, cancellationToken).ConfigureAwait(false);
        // Velopack skips an existing complete file. Reuse its verifier so a damaged cache cannot bypass the hash check.
        await _manager.VerifyDownloadedAsync(update.TargetFullRelease).ConfigureAwait(false);
    }

    public void ApplyOnExit(bool restart)
    {
        var pending = _manager.UpdatePendingRestart ?? throw new InvalidOperationException("No verified update is ready");
        if (pending.PackageId != AppRelease.PackageId) throw new InvalidDataException("Unexpected application package");
        _manager.WaitExitThenApplyUpdates(pending, silent: true, restart: restart);
    }

    internal static void ValidateAsset(VelopackAsset asset)
    {
        if (asset.PackageId != AppRelease.PackageId || asset.Size <= 0 || asset.SHA256?.Length != 64 || !asset.SHA256.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(asset.FileName) || Path.GetFileName(asset.FileName) != asset.FileName
            || asset.FileName.Contains('\\') || asset.FileName.Contains('/') || asset.FileName.Contains(':')
            || !asset.FileName.EndsWith("-full.nupkg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid release package identity, path or SHA256");
    }

    private sealed class UpdateDownloader : HttpClientFileDownloader
    {
        protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
        {
            var client = base.CreateHttpClient(headers, timeout);
            client.Timeout = TimeSpan.FromMinutes(Math.Min(timeout, 1));
            return client;
        }
    }
}
