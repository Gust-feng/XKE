using Xke.Contracts.Responses;

namespace Xke.Core.Course;

public interface ICourseClient
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    string UnavailableMessage { get; }

    Task ConnectAsync(string sessionId, string studentId, CancellationToken cancellationToken = default);

    Task<CourseRoundsFetchResult> LoadRoundsAsync(
        string sessionId,
        string studentId,
        bool refresh,
        CancellationToken cancellationToken = default);

    Task<CoursePreviewFetchResult> LoadPreviewAsync(
        string sessionId,
        string studentId,
        string roundId,
        bool refresh,
        CancellationToken cancellationToken = default);
}
