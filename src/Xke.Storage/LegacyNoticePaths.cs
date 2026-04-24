using System.Text.Json;

namespace Xke.Storage;

public static class LegacyNoticePaths
{
    public static string NormalizeRelativePath(string? path)
    {
        var cleaned = (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(cleaned)) return string.Empty;
        if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return cleaned;
        var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..") return string.Empty;
            normalized.Add(part);
        }
        return string.Join('/', normalized);
    }

    public static string ResolveRelatedRelativePath(string baseRelativePath, string? relatedPath)
    {
        var cleaned = (relatedPath ?? string.Empty).Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return string.Empty;
        if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return cleaned;

        var rawParts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(part => part != ".").ToArray();
        if (rawParts.Length == 0) return string.Empty;
        if ((rawParts[0] == "config" || rawParts[0] == "notice" || rawParts[0] == "main") && !rawParts.Contains(".."))
        {
            return NormalizeRelativePath(cleaned);
        }

        var baseParts = (baseRelativePath ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (baseParts.Count > 0) baseParts.RemoveAt(baseParts.Count - 1);
        foreach (var part in rawParts)
        {
            if (part == "..")
            {
                if (baseParts.Count == 0) return string.Empty;
                baseParts.RemoveAt(baseParts.Count - 1);
                continue;
            }
            baseParts.Add(part);
        }
        return string.Join('/', baseParts);
    }
}
