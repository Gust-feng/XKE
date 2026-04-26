using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeUpdateStore
{
    private const int JobFileAccessAttempts = 5;
    private static readonly TimeSpan JobFileAccessRetryDelay = TimeSpan.FromMilliseconds(20);

    private readonly XkeDataRoot _root;
    private readonly LocalEncryptedFileStore _encrypted = new();

    public XkeUpdateStore(XkeDataRoot root)
    {
        _root = root;
    }

    public string EnsurePackagesDirectory()
    {
        Directory.CreateDirectory(_root.UpdatePackagesDirectory);
        return _root.RelativePath(_root.UpdatePackagesDirectory);
    }

    public string EnsureJobsDirectory()
    {
        Directory.CreateDirectory(_root.UpdateJobsDirectory);
        return _root.RelativePath(_root.UpdateJobsDirectory);
    }

    public string EnsureBackupsDirectory()
    {
        Directory.CreateDirectory(_root.UpdateBackupsDirectory);
        return _root.RelativePath(_root.UpdateBackupsDirectory);
    }

    public string ResolvePackagePath(string fileName)
    {
        var cleaned = XkeDataRoot.SanitizePathSegment(fileName ?? string.Empty);
        if (!cleaned.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            cleaned += ".zip";
        }

        var path = Path.GetFullPath(Path.Combine(_root.UpdatePackagesDirectory, cleaned));
        if (!XkeDataRoot.IsPathInsideRoot(_root.UpdatePackagesDirectory, path))
        {
            throw new InvalidOperationException("更新包路径越界");
        }

        return path;
    }

    public string ResolveTemporaryPackagePath(string jobId)
    {
        var cleaned = XkeDataRoot.SanitizePathSegment(jobId ?? string.Empty);
        var path = Path.GetFullPath(Path.Combine(_root.UpdatePackagesDirectory, $"{cleaned}.download"));
        if (!XkeDataRoot.IsPathInsideRoot(_root.UpdatePackagesDirectory, path))
        {
            throw new InvalidOperationException("更新包临时路径越界");
        }

        return path;
    }

    public string ResolveRunnerDirectory(string runId)
    {
        var cleaned = XkeDataRoot.SanitizePathSegment(runId ?? string.Empty);
        var path = Path.GetFullPath(Path.Combine(_root.UpdateRunnersDirectory, cleaned));
        if (!XkeDataRoot.IsPathInsideRoot(_root.UpdateRunnersDirectory, path))
        {
            throw new InvalidOperationException("更新器运行路径越界");
        }

        return path;
    }

    public string ResolveBackupDirectory(string jobId)
    {
        var cleaned = XkeDataRoot.SanitizePathSegment(jobId ?? string.Empty);
        var path = Path.GetFullPath(Path.Combine(_root.UpdateBackupsDirectory, cleaned));
        if (!XkeDataRoot.IsPathInsideRoot(_root.UpdateBackupsDirectory, path))
        {
            throw new InvalidOperationException("更新备份路径越界");
        }

        return path;
    }

    public XkeUpdateJobDocument LoadJob(string jobId)
    {
        var path = ResolveJobPath(jobId);
        if (!File.Exists(path))
        {
            return new XkeUpdateJobDocument();
        }

        try
        {
            return RetryJobFileAccess(() =>
                _encrypted.ReadJson<XkeUpdateJobDocument>(path) ?? new XkeUpdateJobDocument());
        }
        catch
        {
            return new XkeUpdateJobDocument();
        }
    }

    public XkeUpdateJobDocument? TryLoadJob(string jobId)
    {
        var job = LoadJob(jobId);
        return string.IsNullOrWhiteSpace(job.JobId) ? null : job;
    }

    public string SaveJob(XkeUpdateJobDocument job)
    {
        if (string.IsNullOrWhiteSpace(job.JobId))
        {
            throw new InvalidOperationException("更新任务缺少 job_id");
        }

        var path = ResolveJobPath(job.JobId);
        RetryJobFileAccess(() =>
        {
            _encrypted.WriteJson(path, job);
            return true;
        });
        return _root.RelativePath(path);
    }

    public IReadOnlyList<XkeUpdateJobDocument> LoadJobs()
    {
        if (!Directory.Exists(_root.UpdateJobsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_root.UpdateJobsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                try
                {
                    return RetryJobFileAccess(() => _encrypted.ReadJson<XkeUpdateJobDocument>(path));
                }
                catch
                {
                    return null;
                }
            })
            .Where(job => job is not null && !string.IsNullOrWhiteSpace(job.JobId))
            .Select(job => job!)
            .OrderByDescending(job => job.UpdatedAt, StringComparer.Ordinal)
            .ToArray();
    }

    public XkeUpdateJobDocument? FindLatestVerifiedJob(string latestVersion)
    {
        var version = (latestVersion ?? string.Empty).Trim();
        return LoadJobs().FirstOrDefault(job =>
            string.Equals(job.Status, "verified", StringComparison.OrdinalIgnoreCase)
            && string.Equals(job.LatestVersion, version, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(job.PackageFile)
            && File.Exists(job.PackageFile));
    }

    public XkeUpdateJobDocument? FindLatestJob(string latestVersion)
    {
        var version = (latestVersion ?? string.Empty).Trim();
        return LoadJobs().FirstOrDefault(job =>
            string.IsNullOrWhiteSpace(version)
            || string.Equals(job.LatestVersion, version, StringComparison.OrdinalIgnoreCase));
    }

    public string SaveInstallPlan(XkeUpdateInstallPlanDocument plan)
    {
        ValidateInstallPlanPath(_root.UpdateInstallPlanPath);
        _encrypted.WriteJson(_root.UpdateInstallPlanPath, plan);
        return _root.RelativePath(_root.UpdateInstallPlanPath);
    }

    public XkeUpdateInstallPlanDocument LoadInstallPlan()
    {
        return LoadInstallPlan(_root.UpdateInstallPlanPath);
    }

    public static XkeUpdateInstallPlanDocument LoadInstallPlan(string planPath)
    {
        ValidateInstallPlanPath(planPath);
        if (!File.Exists(planPath))
        {
            return new XkeUpdateInstallPlanDocument();
        }

        try
        {
            return new LocalEncryptedFileStore().ReadJson<XkeUpdateInstallPlanDocument>(planPath)
                   ?? new XkeUpdateInstallPlanDocument();
        }
        catch
        {
            return new XkeUpdateInstallPlanDocument();
        }
    }

    public static void SaveInstallPlan(string planPath, XkeUpdateInstallPlanDocument plan)
    {
        ValidateInstallPlanPath(planPath);
        new LocalEncryptedFileStore().WriteJson(planPath, plan);
    }

    public bool IsInsideUpdateRoot(string path, bool allowRoot = false)
    {
        return XkeDataRoot.IsPathInsideRoot(_root.UpdateDirectory, path, allowRoot);
    }

    private string ResolveJobPath(string jobId)
    {
        var cleaned = XkeDataRoot.SanitizePathSegment(jobId ?? string.Empty);
        var path = Path.GetFullPath(Path.Combine(_root.UpdateJobsDirectory, $"{cleaned}.json"));
        if (!XkeDataRoot.IsPathInsideRoot(_root.UpdateJobsDirectory, path))
        {
            throw new InvalidOperationException("更新任务路径越界");
        }

        return path;
    }

    private static void ValidateInstallPlanPath(string planPath)
    {
        var fullPath = Path.GetFullPath(planPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fullPath)
            || !string.Equals(Path.GetFileName(fullPath), "install-plan.json", StringComparison.OrdinalIgnoreCase)
            || fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .All(segment => !string.Equals(segment, ".updates", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("安装计划路径无效");
        }
    }

    private static T RetryJobFileAccess<T>(Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (IsTransientFileAccess(ex) && attempt < JobFileAccessAttempts)
            {
                Thread.Sleep(JobFileAccessRetryDelay);
            }
        }
    }

    private static bool IsTransientFileAccess(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException;
    }
}
