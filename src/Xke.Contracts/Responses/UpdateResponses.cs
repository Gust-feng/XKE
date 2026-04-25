using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record UpdateStatusPayload(
    [property: JsonPropertyName("current_version")] string CurrentVersion,
    [property: JsonPropertyName("latest_version")] string LatestVersion,
    [property: JsonPropertyName("min_supported_version")] string MinSupportedVersion,
    [property: JsonPropertyName("is_update_available")] bool IsUpdateAvailable,
    [property: JsonPropertyName("force_upgrade")] bool ForceUpgrade,
    [property: JsonPropertyName("package_type")] string PackageType,
    [property: JsonPropertyName("package_downloaded")] bool PackageDownloaded,
    [property: JsonPropertyName("can_install")] bool CanInstall,
    [property: JsonPropertyName("desktop_install_supported")] bool DesktopInstallSupported,
    [property: JsonPropertyName("download_status")] string DownloadStatus,
    [property: JsonPropertyName("download_job_id")] string DownloadJobId,
    [property: JsonPropertyName("install_status")] string InstallStatus,
    [property: JsonPropertyName("install_plan_path")] string InstallPlanPath,
    [property: JsonPropertyName("message")] string Message);

public sealed record UpdateJobPayload(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("latest_version")] string LatestVersion,
    [property: JsonPropertyName("package_type")] string PackageType,
    [property: JsonPropertyName("bytes_received")] long BytesReceived,
    [property: JsonPropertyName("total_bytes")] long? TotalBytes,
    [property: JsonPropertyName("progress_percent")] double ProgressPercent,
    [property: JsonPropertyName("package_size")] long PackageSize,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("updated_at")] string UpdatedAt);

public sealed record UpdatePrepareInstallPayload(
    [property: JsonPropertyName("prepared")] bool Prepared,
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("latest_version")] string LatestVersion,
    [property: JsonPropertyName("install_plan_path")] string InstallPlanPath,
    [property: JsonPropertyName("desktop_install_supported")] bool DesktopInstallSupported,
    [property: JsonPropertyName("message")] string Message);
