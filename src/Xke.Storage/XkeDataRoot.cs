using System.Security.Cryptography;
using System.Text;

namespace Xke.Storage;

public sealed class XkeDataRoot
{
    public XkeDataRoot(string rootPath, string? workspaceRoot = null)
    {
        RootPath = Path.GetFullPath(rootPath);
        WorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? null : Path.GetFullPath(workspaceRoot);
    }

    public string RootPath { get; }
    public string? WorkspaceRoot { get; }
    public string StudentsDirectory => Path.Combine(RootPath, "students");
    public string AuthDirectory => Path.Combine(RootPath, "auth");
    public string CredentialsDirectory => Path.Combine(AuthDirectory, "credentials");
    public string SessionsDirectory => Path.Combine(AuthDirectory, "sessions");
    public string NoticeCacheDirectory => Path.Combine(RootPath, ".notice_cache", LegacyNoticeCacheRoot.CacheVersion);
    public string CloudDirectory => Path.Combine(RootPath, ".cloud");
    public string CloudFeedPath => Path.Combine(CloudDirectory, "feed.json");
    public string CloudFeedMetaPath => Path.Combine(CloudDirectory, "feed.meta.json");
    public string CloudNoticeDirectory => Path.Combine(CloudDirectory, "notice");
    public string CloudNoticeContentDirectory => Path.Combine(CloudNoticeDirectory, "content");
    public string CloudNoticeContentMetaPath => Path.Combine(CloudNoticeDirectory, "content.meta.json");
    public string CloudTelemetryDirectory => Path.Combine(CloudDirectory, "telemetry");
    public string CloudTelemetryInstallationPath => Path.Combine(CloudTelemetryDirectory, "installation.json");
    public string CloudTelemetryKeyPath => Path.Combine(CloudTelemetryDirectory, "telemetry_aes_key.json");
    public string CloudTelemetrySpoolDirectory => Path.Combine(CloudTelemetryDirectory, "spool");
    public string UpdateDirectory => Path.Combine(RootPath, ".updates");
    public string UpdatePackagesDirectory => Path.Combine(UpdateDirectory, "packages");
    public string UpdateJobsDirectory => Path.Combine(UpdateDirectory, "jobs");
    public string UpdateBackupsDirectory => Path.Combine(UpdateDirectory, "backups");
    public string UpdateRunnersDirectory => Path.Combine(UpdateDirectory, "runners");
    public string UpdateInstallPlanPath => Path.Combine(UpdateDirectory, "install-plan.json");
    public string DesktopDirectory => Path.Combine(RootPath, ".desktop");
    public string DesktopSettingsPath => Path.Combine(DesktopDirectory, "desktop_settings.json");
    public string TelemetryDirectory => CloudTelemetryDirectory;
    public string TelemetryInstallationPath => CloudTelemetryInstallationPath;
    public string TelemetryKeyPath => CloudTelemetryKeyPath;
    public string TelemetrySpoolDirectory => CloudTelemetrySpoolDirectory;
    public string FeedbackDirectory => Path.Combine(RootPath, ".feedback");
    public string FeedbackOutboxDirectory => Path.Combine(FeedbackDirectory, "outbox");
    public string FeedbackUsageDirectory => Path.Combine(FeedbackDirectory, "usage");

    public XkeStudentStore ForStudent(string studentId)
    {
        return new(this, SanitizePathSegment(studentId));
    }

    public XkeCredentialStore Credentials()
    {
        return new(this);
    }

    public XkeSessionStore Sessions()
    {
        return new(this);
    }

    public LegacyNoticeCacheRoot NoticeCache()
    {
        return new(NoticeCacheDirectory);
    }

    public CloudSyncStore CloudSync()
    {
        return new(this);
    }

    public XkeUpdateStore Updates()
    {
        return new(this);
    }

    public XkeDesktopSettingsStore DesktopSettings()
    {
        return new(this);
    }

    public XkeDesktopRuntimeStore DesktopRuntime()
    {
        return new(this);
    }

    public XkeTelemetryStore Telemetry()
    {
        return new(this);
    }

    public XkeFeedbackOutboxStore FeedbackOutbox()
    {
        return new(this);
    }

    public XkeFeedbackUsageStore FeedbackUsage()
    {
        return new(this);
    }

    public string RelativePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        foreach (var candidate in new[] { WorkspaceRoot, RootPath })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var root = Path.GetFullPath(candidate);
            if (IsPathInsideRoot(root, fullPath, allowRoot: true))
            {
                return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            }
        }

        return fullPath.Replace('\\', '/');
    }

    public bool IsInsideDataRoot(string path, bool allowRoot = false)
    {
        return IsPathInsideRoot(RootPath, path, allowRoot);
    }

    public static bool IsPathInsideRoot(string rootPath, string candidatePath, bool allowRoot = false)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(rootPath), Path.GetFullPath(candidatePath));
        return (allowRoot && relative == ".")
               || (!Path.IsPathRooted(relative)
                   && relative != "."
                   && relative != ".."
                   && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                   && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }

    public static string SanitizePathSegment(string value)
    {
        var raw = string.Concat((value ?? string.Empty).Trim().Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'
                ? ch
                : '_'));
        return string.IsNullOrWhiteSpace(raw) ? "unknown" : raw;
    }

    internal static string HashSessionId(string sessionId)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId ?? string.Empty))).ToLowerInvariant();
    }
}
