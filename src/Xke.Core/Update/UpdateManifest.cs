using System.Text.RegularExpressions;

namespace Xke.Core.Update;

public sealed record UpdateManifest(
    string UpdatedAt,
    string LatestVersion,
    string MinSupportedVersion,
    string DownloadUrl,
    string PackageSha256,
    long PackageSize,
    string PackageType,
    string ReleaseNotesUrl,
    bool ForceUpgrade,
    IReadOnlyList<string> ValidationErrors)
{
    public bool HasUpdatePackage =>
        ValidationErrors.Count == 0
        && !string.IsNullOrWhiteSpace(LatestVersion)
        && !string.IsNullOrWhiteSpace(DownloadUrl)
        && !string.IsNullOrWhiteSpace(PackageSha256);
}

public static class UpdateManifestParser
{
    private static readonly Regex Sha256Pattern = new("^[a-fA-F0-9]{64}$", RegexOptions.Compiled);

    public static UpdateManifest FromFeed(Dictionary<string, object?> feed)
    {
        var updateInfo = GetDictionary(feed, "update_info");
        if (updateInfo.Count == 0)
        {
            var config = GetDictionary(feed, "config");
            updateInfo = GetDictionary(config, "update_info");
        }

        return FromUpdateInfo(updateInfo);
    }

    public static UpdateManifest FromUpdateInfo(Dictionary<string, object?> updateInfo)
    {
        var latestVersion = Truncate(Text(updateInfo.GetValueOrDefault("latest_version")), 64);
        var minSupportedVersion = Truncate(Text(updateInfo.GetValueOrDefault("min_supported_version")), 64);
        var downloadUrl = Truncate(Text(updateInfo.GetValueOrDefault("download_url")), 2000);
        var packageSha256 = Text(updateInfo.GetValueOrDefault("package_sha256")).ToLowerInvariant();
        var packageType = Text(updateInfo.GetValueOrDefault("package_type")).ToLowerInvariant();
        var releaseNotesUrl = Truncate(Text(updateInfo.GetValueOrDefault("release_notes_url")), 2000);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(packageType))
        {
            packageType = "zip";
        }

        var packageSize = Long(updateInfo.GetValueOrDefault("package_size"), 0);
        if (!string.IsNullOrWhiteSpace(downloadUrl)
            && (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("download_url must be an HTTPS URL");
        }

        if (!string.IsNullOrWhiteSpace(packageSha256) && !Sha256Pattern.IsMatch(packageSha256))
        {
            errors.Add("package_sha256 must be a 64-character hex string");
        }

        if (!string.Equals(packageType, "zip", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("package_type must be zip");
        }

        if (packageSize < 0)
        {
            errors.Add("package_size must not be negative");
            packageSize = 0;
        }

        return new UpdateManifest(
            UpdatedAt: Truncate(Text(updateInfo.GetValueOrDefault("updated_at")), 64),
            LatestVersion: latestVersion,
            MinSupportedVersion: minSupportedVersion,
            DownloadUrl: downloadUrl,
            PackageSha256: packageSha256,
            PackageSize: packageSize,
            PackageType: packageType,
            ReleaseNotesUrl: releaseNotesUrl,
            ForceUpgrade: updateInfo.GetValueOrDefault("force_upgrade") is true,
            ValidationErrors: errors);
    }

    private static Dictionary<string, object?> GetDictionary(Dictionary<string, object?> parent, string key)
    {
        return parent.GetValueOrDefault(key) is Dictionary<string, object?> dictionary ? dictionary : [];
    }

    private static string Text(object? value)
    {
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string Truncate(string value, int maxLength)
    {
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static long Long(object? value, long fallback)
    {
        return value switch
        {
            int number => number,
            long number => number,
            double number => (long)number,
            _ when long.TryParse(value?.ToString(), out var parsed) => parsed,
            _ => fallback,
        };
    }
}
