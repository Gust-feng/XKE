using System.Diagnostics;
using System.IO.Compression;
using Xke.Contracts.Models;
using Xke.Storage;

namespace Xke.Core.Update;

public sealed record UpdateInstallerOptions(
    int ParentProcessId,
    string PlanPath,
    string MainExecutablePath,
    bool RestartAfterInstall = true,
    TimeSpan? WaitTimeout = null);

public sealed record UpdateInstallerResult(
    bool Ok,
    string Status,
    string Message,
    string BackupDirectory);

public sealed class UpdateInstaller
{
    private static readonly HashSet<string> ProtectedRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".runtime",
        ".desktop",
        ".updates",
        "data",
    };

    public async Task<UpdateInstallerResult> InstallAsync(
        UpdateInstallerOptions options,
        CancellationToken cancellationToken = default)
    {
        var planPath = Path.GetFullPath(options.PlanPath);
        var plan = XkeUpdateStore.LoadInstallPlan(planPath);
        if (string.IsNullOrWhiteSpace(plan.JobId))
        {
            throw new InvalidOperationException("安装计划为空或不可读取");
        }

        await WaitForParentProcessAsync(options.ParentProcessId, options.WaitTimeout ?? TimeSpan.FromSeconds(120), cancellationToken)
            .ConfigureAwait(false);

        var validated = await ValidatePlanAsync(plan, options.MainExecutablePath, cancellationToken).ConfigureAwait(false);
        var workingRoot = Path.Combine(Path.GetDirectoryName(plan.BackupDirectory) ?? Path.GetTempPath(), $"stage-{plan.JobId}");
        var stageRoot = Path.Combine(workingRoot, "extract");

        try
        {
            UpdatePlanStatus(planPath, plan, "installing", string.Empty);
            CleanDirectory(workingRoot);
            Directory.CreateDirectory(stageRoot);
            Directory.CreateDirectory(plan.BackupDirectory);

            ZipFile.ExtractToDirectory(validated.PackagePath, stageRoot);
            var payloadRoot = ResolvePayloadRoot(stageRoot);

            BackupInstallDirectory(validated.InstallDirectory, plan.BackupDirectory);
            DeleteInstallEntries(validated.InstallDirectory);
            CopyDirectoryContents(payloadRoot, validated.InstallDirectory, skipProtectedRoots: true);

            UpdatePlanStatus(planPath, plan, "installed", string.Empty);
            if (options.RestartAfterInstall)
            {
                StartMainExecutable(validated.AppExecutablePath);
            }

            return new UpdateInstallerResult(true, "installed", "安装完成", plan.BackupDirectory);
        }
        catch (Exception ex)
        {
            try
            {
                RestoreBackup(validated.InstallDirectory, plan.BackupDirectory);
            }
            catch (Exception restoreEx)
            {
                UpdatePlanStatus(planPath, plan, "failed", $"{ex.Message}；回滚失败：{restoreEx.Message}");
                return new UpdateInstallerResult(false, "failed", $"安装失败且回滚失败：{restoreEx.Message}", plan.BackupDirectory);
            }

            UpdatePlanStatus(planPath, plan, "failed", ex.Message);
            return new UpdateInstallerResult(false, "failed", ex.Message, plan.BackupDirectory);
        }
        finally
        {
            CleanDirectory(workingRoot);
        }
    }

    private static async Task<ValidatedPlan> ValidatePlanAsync(
        XkeUpdateInstallPlanDocument plan,
        string mainExecutableOverride,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(plan.PackageType, "zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("第一版更新器只支持 ZIP 完整包");
        }

        var packagePath = Path.GetFullPath(plan.PackagePath);
        var installDirectory = Path.GetFullPath(plan.InstallDirectory);
        var appExecutablePath = string.IsNullOrWhiteSpace(mainExecutableOverride)
            ? Path.GetFullPath(plan.AppExecutablePath)
            : Path.GetFullPath(mainExecutableOverride);

        if (!File.Exists(packagePath))
        {
            throw new InvalidOperationException("更新包不存在");
        }

        if (!Directory.Exists(installDirectory))
        {
            throw new InvalidOperationException("安装目录不存在");
        }

        if (!XkeDataRoot.IsPathInsideRoot(installDirectory, appExecutablePath))
        {
            throw new InvalidOperationException("主程序路径不在安装目录内");
        }

        if (Path.GetPathRoot(installDirectory) == installDirectory)
        {
            throw new InvalidOperationException("安装目录不能是磁盘根目录");
        }

        var verification = await UpdatePackageVerifier.VerifyAsync(
            packagePath,
            plan.PackageSha256,
            plan.PackageSize,
            cancellationToken).ConfigureAwait(false);
        if (!verification.Ok)
        {
            throw new InvalidOperationException(verification.Error);
        }

        return new ValidatedPlan(packagePath, installDirectory, appExecutablePath);
    }

    private static async Task WaitForParentProcessAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (processId <= 0 || processId == Environment.ProcessId)
        {
            return;
        }

        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch
        {
            return;
        }

        await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolvePayloadRoot(string stageRoot)
    {
        var entries = Directory.EnumerateFileSystemEntries(stageRoot).ToArray();
        if (entries.Length == 1 && Directory.Exists(entries[0]))
        {
            return entries[0];
        }

        var portablePackageRoots = entries
            .Where(Directory.Exists)
            .Where(entry => File.Exists(Path.Combine(entry, "Xke.DesktopHost.exe")))
            .ToArray();
        if (portablePackageRoots.Length == 1)
        {
            return portablePackageRoots[0];
        }

        return stageRoot;
    }

    private static void BackupInstallDirectory(string installDirectory, string backupDirectory)
    {
        CleanDirectory(backupDirectory);
        Directory.CreateDirectory(backupDirectory);
        CopyDirectoryContents(installDirectory, backupDirectory, skipProtectedRoots: true);
    }

    private static void DeleteInstallEntries(string installDirectory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(installDirectory))
        {
            if (IsProtectedRootEntry(entry))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }
    }

    private static void RestoreBackup(string installDirectory, string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory))
        {
            return;
        }

        DeleteInstallEntries(installDirectory);
        CopyDirectoryContents(backupDirectory, installDirectory, skipProtectedRoots: true);
    }

    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory, bool skipProtectedRoots)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourceEntry in Directory.EnumerateFileSystemEntries(sourceDirectory))
        {
            if (skipProtectedRoots && IsProtectedRootEntry(sourceEntry))
            {
                continue;
            }

            var name = Path.GetFileName(sourceEntry);
            var destinationEntry = Path.Combine(destinationDirectory, name);
            if (Directory.Exists(sourceEntry))
            {
                CopyDirectoryContents(sourceEntry, destinationEntry, skipProtectedRoots: false);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationEntry)!);
                File.Copy(sourceEntry, destinationEntry, overwrite: true);
            }
        }
    }

    private static bool IsProtectedRootEntry(string path)
    {
        return ProtectedRootNames.Contains(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
    }

    private static void StartMainExecutable(string executablePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
        });
    }

    private static void UpdatePlanStatus(string planPath, XkeUpdateInstallPlanDocument plan, string status, string error)
    {
        XkeUpdateStore.SaveInstallPlan(planPath, plan with
        {
            Status = status,
            Error = error,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
    }

    private static void CleanDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        Directory.Delete(directory, recursive: true);
    }

    private sealed record ValidatedPlan(
        string PackagePath,
        string InstallDirectory,
        string AppExecutablePath);
}
