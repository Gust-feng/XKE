using System.Reflection;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Core.Notice;
using Xke.Storage;

namespace Xke.Core.Update;

public interface IUpdateVersionProvider
{
    string CurrentVersion { get; }
}

public sealed class AssemblyUpdateVersionProvider : IUpdateVersionProvider
{
    public string CurrentVersion
    {
        get
        {
            var assembly = typeof(AssemblyUpdateVersionProvider).Assembly;
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion.Split('+')[0].Trim();
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
    }
}

public sealed class UpdateService
{
    private readonly XkeDataRoot _dataRoot;
    private readonly NoticeService _noticeService;
    private readonly IUpdatePackageDownloader _downloader;
    private readonly IUpdateVersionProvider _versionProvider;
    private readonly SemaphoreSlim _downloadGate = new(1, 1);

    public UpdateService(
        XkeDataRoot dataRoot,
        NoticeService noticeService,
        IUpdatePackageDownloader downloader,
        IUpdateVersionProvider versionProvider)
    {
        _dataRoot = dataRoot;
        _noticeService = noticeService;
        _downloader = downloader;
        _versionProvider = versionProvider;
    }

    public async Task<UpdateStatusPayload> GetStatusAsync(
        bool desktopInstallSupported,
        string trigger = "view",
        CancellationToken cancellationToken = default)
    {
        var manifest = await LoadManifestAsync(trigger, cancellationToken).ConfigureAwait(false);
        var store = _dataRoot.Updates();
        var updateAvailable = IsUpdateAvailable(manifest);
        var forceUpgrade = IsForceUpgrade(manifest);
        var shouldExposeUpdateJob = updateAvailable || forceUpgrade;
        var verifiedJob = shouldExposeUpdateJob ? store.FindLatestVerifiedJob(manifest.LatestVersion) : null;
        var latestJob = shouldExposeUpdateJob ? verifiedJob ?? store.FindLatestJob(manifest.LatestVersion) : null;
        var plan = store.LoadInstallPlan();
        var installStatus = string.IsNullOrWhiteSpace(plan.Status) ? string.Empty : plan.Status;
        var planMatches = !string.IsNullOrWhiteSpace(plan.JobId)
                          && latestJob is not null
                          && string.Equals(plan.JobId, latestJob.JobId, StringComparison.Ordinal);
        var canInstall = verifiedJob is not null
                         && desktopInstallSupported
                         && (string.IsNullOrWhiteSpace(plan.Status)
                             || planMatches
                             || !string.Equals(plan.Status, "installing", StringComparison.OrdinalIgnoreCase));

        return new UpdateStatusPayload(
            CurrentVersion: _versionProvider.CurrentVersion,
            LatestVersion: manifest.LatestVersion,
            MinSupportedVersion: manifest.MinSupportedVersion,
            IsUpdateAvailable: updateAvailable,
            ForceUpgrade: forceUpgrade,
            PackageType: manifest.PackageType,
            PackageDownloaded: verifiedJob is not null,
            CanInstall: canInstall,
            DesktopInstallSupported: desktopInstallSupported,
            DownloadStatus: latestJob?.Status ?? string.Empty,
            DownloadJobId: latestJob?.JobId ?? string.Empty,
            InstallStatus: installStatus,
            InstallPlanPath: planMatches ? _dataRoot.UpdateInstallPlanPath : string.Empty,
            Message: BuildStatusMessage(manifest, latestJob, desktopInstallSupported));
    }

    public async Task<UpdateJobPayload> StartDownloadAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await LoadManifestAsync("manual/update", cancellationToken).ConfigureAwait(false);
        if (!IsUpdateAvailable(manifest) && !IsForceUpgrade(manifest))
        {
            throw new InvalidOperationException("当前版本不需要下载更新");
        }

        if (!manifest.HasUpdatePackage)
        {
            throw new InvalidOperationException(manifest.ValidationErrors.Count > 0
                ? string.Join("；", manifest.ValidationErrors)
                : "云端更新信息缺少完整更新包字段");
        }

        await _downloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = _dataRoot.Updates();
            var verifiedJob = store.FindLatestVerifiedJob(manifest.LatestVersion);
            if (verifiedJob is not null)
            {
                return ToJobPayload(verifiedJob);
            }

            var activeJob = store.FindLatestJob(manifest.LatestVersion);
            if (activeJob is not null && activeJob.Status is "downloading" or "verifying")
            {
                return ToJobPayload(activeJob);
            }

            store.EnsurePackagesDirectory();
            store.EnsureJobsDirectory();
            var now = Now();
            var job = new XkeUpdateJobDocument
            {
                JobId = Guid.NewGuid().ToString("N"),
                Status = "pending",
                LatestVersion = manifest.LatestVersion,
                PackageType = manifest.PackageType,
                PackageSha256 = manifest.PackageSha256,
                PackageSize = manifest.PackageSize,
                TotalBytes = manifest.PackageSize > 0 ? manifest.PackageSize : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            store.SaveJob(job);
            _ = Task.Run(() => RunDownloadJobAsync(job.JobId, manifest, CancellationToken.None), CancellationToken.None);
            return ToJobPayload(job);
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    public UpdateJobPayload GetJob(string jobId)
    {
        var job = _dataRoot.Updates().TryLoadJob(jobId)
                  ?? throw new InvalidOperationException("更新任务不存在");
        return ToJobPayload(job);
    }

    public async Task<UpdatePrepareInstallPayload> PrepareInstallAsync(
        bool desktopInstallSupported,
        string appExecutablePath,
        CancellationToken cancellationToken = default)
    {
        if (!desktopInstallSupported)
        {
            return new UpdatePrepareInstallPayload(false, string.Empty, string.Empty, string.Empty, false, "当前环境不支持一键安装");
        }

        var manifest = await LoadManifestAsync("manual/update", cancellationToken).ConfigureAwait(false);
        if (!IsUpdateAvailable(manifest) && !IsForceUpgrade(manifest))
        {
            throw new InvalidOperationException("当前版本不需要安装更新");
        }

        var store = _dataRoot.Updates();
        var job = store.FindLatestVerifiedJob(manifest.LatestVersion)
                  ?? throw new InvalidOperationException("更新包尚未下载或校验未通过");
        var executablePath = Path.GetFullPath(appExecutablePath);
        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException("主程序路径不存在，无法准备安装");
        }

        var installDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            throw new InvalidOperationException("安装目录不存在，无法准备安装");
        }

        if (!CanWriteDirectory(installDirectory))
        {
            return new UpdatePrepareInstallPayload(false, job.JobId, job.LatestVersion, string.Empty, true, "安装目录不可写，需要手动更新或管理员安装");
        }

        var now = Now();
        var plan = new XkeUpdateInstallPlanDocument
        {
            JobId = job.JobId,
            LatestVersion = job.LatestVersion,
            PackageType = job.PackageType,
            PackagePath = job.PackageFile,
            PackageSha256 = job.PackageSha256,
            PackageSize = job.PackageSize,
            InstallDirectory = Path.GetFullPath(installDirectory),
            AppExecutablePath = executablePath,
            BackupDirectory = store.ResolveBackupDirectory(job.JobId),
            Status = "prepared",
            CreatedAt = now,
            UpdatedAt = now,
        };

        var planPath = store.SaveInstallPlan(plan);
        store.SaveJob(job with { InstallPlanPath = planPath, UpdatedAt = now });

        return new UpdatePrepareInstallPayload(
            Prepared: true,
            JobId: job.JobId,
            LatestVersion: job.LatestVersion,
            InstallPlanPath: _dataRoot.UpdateInstallPlanPath,
            DesktopInstallSupported: true,
            Message: "安装计划已准备好");
    }

    private async Task RunDownloadJobAsync(string jobId, UpdateManifest manifest, CancellationToken cancellationToken)
    {
        var store = _dataRoot.Updates();
        var tempPath = store.ResolveTemporaryPackagePath(jobId);
        var packagePath = store.ResolvePackagePath($"xke-{manifest.LatestVersion}-{manifest.PackageSha256[..12]}.zip");
        try
        {
            var job = store.LoadJob(jobId) with
            {
                Status = "downloading",
                PackageFile = packagePath,
                UpdatedAt = Now(),
            };
            store.SaveJob(job);

            await _downloader.DownloadAsync(
                new Uri(manifest.DownloadUrl, UriKind.Absolute),
                tempPath,
                (received, total, token) =>
                {
                    var current = store.LoadJob(jobId);
                    store.SaveJob(current with
                    {
                        Status = "downloading",
                        BytesReceived = received,
                        TotalBytes = total ?? current.TotalBytes,
                        UpdatedAt = Now(),
                    });
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);

            job = store.LoadJob(jobId) with { Status = "verifying", UpdatedAt = Now() };
            store.SaveJob(job);

            var verification = await UpdatePackageVerifier.VerifyAsync(
                tempPath,
                manifest.PackageSha256,
                manifest.PackageSize,
                cancellationToken).ConfigureAwait(false);
            if (!verification.Ok)
            {
                throw new InvalidDataException(verification.Error);
            }

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }
            File.Move(tempPath, packagePath);
            store.SaveJob(store.LoadJob(jobId) with
            {
                Status = "verified",
                PackageFile = packagePath,
                PackageSha256 = verification.Sha256,
                PackageSize = verification.Size,
                BytesReceived = verification.Size,
                TotalBytes = verification.Size,
                VerifiedAt = Now(),
                UpdatedAt = Now(),
            });
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            store.SaveJob(store.LoadJob(jobId) with
            {
                Status = "failed",
                Error = ex.Message,
                UpdatedAt = Now(),
            });
        }
    }

    private async Task<UpdateManifest> LoadManifestAsync(string trigger, CancellationToken cancellationToken)
    {
        var feed = await _noticeService.LoadFeedAsync(
            "beta",
            _versionProvider.CurrentVersion,
            trigger,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return UpdateManifestParser.FromFeed(feed);
    }

    private bool IsUpdateAvailable(UpdateManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(manifest.LatestVersion)
               && UpdateVersionComparer.Compare(_versionProvider.CurrentVersion, manifest.LatestVersion) < 0;
    }

    private bool IsForceUpgrade(UpdateManifest manifest)
    {
        return manifest.ForceUpgrade
               || (!string.IsNullOrWhiteSpace(manifest.MinSupportedVersion)
                   && UpdateVersionComparer.Compare(_versionProvider.CurrentVersion, manifest.MinSupportedVersion) < 0);
    }

    private string BuildStatusMessage(UpdateManifest manifest, XkeUpdateJobDocument? job, bool desktopInstallSupported)
    {
        if (manifest.ValidationErrors.Count > 0)
        {
            return "更新信息暂不可用";
        }

        if (!desktopInstallSupported)
        {
            return "当前环境不支持一键安装";
        }

        if (job is not null && string.Equals(job.Status, "verified", StringComparison.OrdinalIgnoreCase))
        {
            return "更新包已下载，可安装并重启";
        }

        if (job is not null && string.Equals(job.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(job.Error) ? "下载失败" : job.Error;
        }

        if (IsUpdateAvailable(manifest) || IsForceUpgrade(manifest))
        {
            return "发现新版本，可下载更新";
        }

        return "当前已是最新版本";
    }

    private static UpdateJobPayload ToJobPayload(XkeUpdateJobDocument job)
    {
        var total = job.TotalBytes ?? (job.PackageSize > 0 ? job.PackageSize : null);
        var totalValue = total.GetValueOrDefault();
        var progress = totalValue > 0
            ? Math.Clamp(job.BytesReceived * 100d / totalValue, 0d, 100d)
            : 0d;
        return new UpdateJobPayload(
            JobId: job.JobId,
            Status: job.Status,
            LatestVersion: job.LatestVersion,
            PackageType: job.PackageType,
            BytesReceived: job.BytesReceived,
            TotalBytes: total,
            ProgressPercent: Math.Round(progress, 2),
            PackageSize: job.PackageSize,
            Error: job.Error,
            UpdatedAt: job.UpdatedAt);
    }

    private static bool CanWriteDirectory(string directory)
    {
        try
        {
            var probePath = Path.Combine(directory, $".xke-write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only; the failed job state is the source of truth.
        }
    }

    private static string Now()
    {
        return DateTimeOffset.Now.ToString("O");
    }
}
