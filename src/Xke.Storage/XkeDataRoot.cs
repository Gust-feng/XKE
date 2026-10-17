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
    public string JwSessionsDirectory => Path.Combine(AuthDirectory, "jw-sessions");
    public string DesktopDirectory => Path.Combine(RootPath, ".desktop");
    public string DesktopSettingsPath => Path.Combine(DesktopDirectory, "desktop_settings.json");

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

    public XkeJwSessionStore JwSessions()
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
