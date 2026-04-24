using Xke.Contracts.Responses;

namespace Xke.Core.Course;

public sealed record CourseRoundsFetchResult(
    IReadOnlyList<CourseRound> Rounds,
    IReadOnlyList<string> Warnings,
    Dictionary<string, object?> RawPayload
);

public sealed record CoursePreviewFetchResult(
    string RoundId,
    IReadOnlyList<CourseItem> Courses,
    IReadOnlyList<string> Warnings,
    Dictionary<string, object?> RawPayload
);
