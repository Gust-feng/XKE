using Xke.Contracts.Responses;

namespace Xke.Core.Course;

public interface ICourseClient
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    string UnavailableMessage { get; }

    Task ConnectAsync(string studentId, CancellationToken cancellationToken = default);

    Task<CourseRoundsFetchResult> LoadRoundsAsync(
        string studentId,
        bool refresh,
        CancellationToken cancellationToken = default);

    Task<CoursePreviewFetchResult> LoadPreviewAsync(
        string studentId,
        string roundId,
        bool refresh,
        CancellationToken cancellationToken = default);
}
