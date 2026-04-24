namespace Xke.Core.Course;

public sealed class UnavailableCourseClient : ICourseClient
{
    public const string DeferredReason = CourseDeferredPolicy.TraditionalJwCaptchaReason;
    public const string DeferredMessage = CourseDeferredPolicy.TraditionalJwCaptchaMessage;

    public bool IsAvailable => false;
    public string UnavailableReason => DeferredReason;
    public string UnavailableMessage => DeferredMessage;

    public Task ConnectAsync(string studentId, CancellationToken cancellationToken = default)
    {
        throw new CourseException(501, UnavailableMessage, UnavailableReason);
    }

    public Task<CourseRoundsFetchResult> LoadRoundsAsync(string studentId, bool refresh, CancellationToken cancellationToken = default)
    {
        throw new CourseException(501, UnavailableMessage, UnavailableReason);
    }

    public Task<CoursePreviewFetchResult> LoadPreviewAsync(string studentId, string roundId, bool refresh, CancellationToken cancellationToken = default)
    {
        throw new CourseException(501, UnavailableMessage, UnavailableReason);
    }
}
