using System.Text.Json.Serialization;

namespace Xke.Contracts.Models;

public sealed record XkeUpdateJobDocument
{
    [JsonPropertyName("job_id")]
    public string JobId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("latest_version")]
    public string LatestVersion { get; init; } = string.Empty;

    [JsonPropertyName("package_type")]
    public string PackageType { get; init; } = "zip";

    [JsonPropertyName("package_file")]
    public string PackageFile { get; init; } = string.Empty;

    [JsonPropertyName("package_sha256")]
    public string PackageSha256 { get; init; } = string.Empty;

    [JsonPropertyName("package_size")]
    public long PackageSize { get; init; }

    [JsonPropertyName("bytes_received")]
    public long BytesReceived { get; init; }

    [JsonPropertyName("total_bytes")]
    public long? TotalBytes { get; init; }

    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;

    [JsonPropertyName("verified_at")]
    public string VerifiedAt { get; init; } = string.Empty;

    [JsonPropertyName("install_plan_path")]
    public string InstallPlanPath { get; init; } = string.Empty;
}

public sealed record XkeUpdateInstallPlanDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1";

    [JsonPropertyName("job_id")]
    public string JobId { get; init; } = string.Empty;

    [JsonPropertyName("latest_version")]
    public string LatestVersion { get; init; } = string.Empty;

    [JsonPropertyName("package_type")]
    public string PackageType { get; init; } = "zip";

    [JsonPropertyName("package_path")]
    public string PackagePath { get; init; } = string.Empty;

    [JsonPropertyName("package_sha256")]
    public string PackageSha256 { get; init; } = string.Empty;

    [JsonPropertyName("package_size")]
    public long PackageSize { get; init; }

    [JsonPropertyName("install_directory")]
    public string InstallDirectory { get; init; } = string.Empty;

    [JsonPropertyName("app_executable_path")]
    public string AppExecutablePath { get; init; } = string.Empty;

    [JsonPropertyName("backup_directory")]
    public string BackupDirectory { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "prepared";

    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;
}
