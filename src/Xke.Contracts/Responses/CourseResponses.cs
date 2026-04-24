using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record CourseConnectionState(
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("updated_at")] string UpdatedAt
);

public sealed record CourseFeatureState(
    [property: JsonPropertyName("rounds")] bool Rounds,
    [property: JsonPropertyName("preview")] bool Preview,
    [property: JsonPropertyName("grab")] bool Grab,
    [property: JsonPropertyName("grab_reason")] string GrabReason
);

public sealed record CourseGrabDeferredState(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("running")] bool Running,
    [property: JsonPropertyName("deferred")] bool Deferred,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("message")] string Message
);

public sealed record CourseStatusPayload(
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("storage_dir")] string StorageDir,
    [property: JsonPropertyName("course_connection")] CourseConnectionState CourseConnection,
    [property: JsonPropertyName("features")] CourseFeatureState Features,
    [property: JsonPropertyName("grab_state")] CourseGrabDeferredState GrabState
);

public sealed record CourseConnectPayload(
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("storage_dir")] string StorageDir,
    [property: JsonPropertyName("course_connection")] CourseConnectionState CourseConnection
);

public sealed record CourseRound(
    [property: JsonPropertyName("round_id")] string RoundId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("term")] string Term,
    [property: JsonPropertyName("time_range")] string TimeRange,
    [property: JsonPropertyName("status")] string Status
);

public sealed record CourseRoundsPayload(
    [property: JsonPropertyName("rounds")] IReadOnlyList<CourseRound> Rounds,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("cached")] bool Cached,
    [property: JsonPropertyName("updated_at")] string UpdatedAt,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings
);

public sealed record CoursePreviewPayload(
    [property: JsonPropertyName("round_id")] string RoundId,
    [property: JsonPropertyName("courses")] IReadOnlyList<CourseItem> Courses,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("cached")] bool Cached,
    [property: JsonPropertyName("updated_at")] string UpdatedAt,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings
);

public sealed record CourseDeferredPayload(
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("grab_state")] CourseGrabDeferredState GrabState
);
