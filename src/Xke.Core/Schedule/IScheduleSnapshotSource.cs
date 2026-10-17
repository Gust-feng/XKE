using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

public interface IScheduleSnapshotSource
{
    Task<ScheduleSnapshotAttempt> FetchAsync(string studentId, ScheduleRequest request, CancellationToken cancellationToken);

    Task<ScheduleSnapshotAttempt> FetchSemesterAsync(string studentId, string academicTerm, CancellationToken cancellationToken)
        => Task.FromResult(ScheduleSnapshotAttempt.Unavailable(string.Empty));
}

public sealed record ScheduleSnapshotAttempt(JwScheduleFetchResult? Schedule, string FallbackReason)
{
    public static ScheduleSnapshotAttempt Unavailable(string reason) => new(null, reason);
}
