namespace Xke.Core.Runtime;

public sealed record XkeResolvedPaths(
    string AppBaseDirectory,
    string? RepoRoot,
    string? WebRootPath)
{
    public bool HasWebRoot => !string.IsNullOrWhiteSpace(WebRootPath);
}

public static class XkePathResolver
{
    public static XkeResolvedPaths Resolve(string? explicitWebRootPath = null, string? startDirectory = null)
    {
        var appBaseDirectory = Path.GetFullPath(startDirectory ?? AppContext.BaseDirectory);
        var webRootPath = FindWebRoot(explicitWebRootPath, appBaseDirectory);
        var repoRoot = webRootPath is null ? null : Directory.GetParent(webRootPath)?.FullName;
        return new XkeResolvedPaths(appBaseDirectory, repoRoot, webRootPath);
    }

    public static string? FindWebRoot(string? explicitWebRootPath = null, string? startDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitWebRootPath))
        {
            var explicitPath = Path.GetFullPath(explicitWebRootPath);
            return File.Exists(Path.Combine(explicitPath, "index.html"))
                ? explicitPath
                : null;
        }

        foreach (var candidate in EnumerateWebRootCandidates(explicitWebRootPath, startDirectory ?? AppContext.BaseDirectory))
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "index.html")))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateWebRootCandidates(string? explicitWebRootPath, string startDirectory)
    {
        var envWebRoot = Environment.GetEnvironmentVariable("XKE_WEB_ROOT");
        if (!string.IsNullOrWhiteSpace(envWebRoot))
        {
            yield return envWebRoot;
        }

        foreach (var ancestor in EnumerateAncestors(Path.GetFullPath(startDirectory)))
        {
            yield return Path.Combine(ancestor, "frontend", "dist");
            yield return Path.Combine(ancestor, "web");
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string directory)
    {
        var current = new DirectoryInfo(directory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}
