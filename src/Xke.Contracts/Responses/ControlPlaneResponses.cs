using System.Text.Json.Serialization;
using Xke.Contracts.Models;

namespace Xke.Contracts.Responses;

public sealed record UpdatesFeedPayload(
    [property: JsonPropertyName("feed")] IReadOnlyDictionary<string, object?> Feed,
    [property: JsonPropertyName("student_id")] string StudentId
);

public sealed record DesktopSettingsPayload(
    [property: JsonPropertyName("settings")] DesktopSettingsDocument Settings,
    [property: JsonPropertyName("startup_registered")] bool? StartupRegistered
);

public sealed record AccessBlockedDetail(
    [property: JsonPropertyName("blocked")] bool Blocked,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("student_id_hash")] string StudentIdHash,
    [property: JsonPropertyName("reason")] string Reason
);
