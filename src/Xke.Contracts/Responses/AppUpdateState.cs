using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record AppUpdateState(
    [property: JsonPropertyName("current_version")] string CurrentVersion,
    [property: JsonPropertyName("repository_url")] string RepositoryUrl,
    [property: JsonPropertyName("release_url")] string ReleaseUrl,
    [property: JsonPropertyName("latest_version")] string? LatestVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("progress")] int Progress,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("checked_at")] string? CheckedAt);
