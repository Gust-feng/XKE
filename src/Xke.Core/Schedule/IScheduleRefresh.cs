namespace Xke.Core.Schedule;

public interface IScheduleRefresh
{
    Task RefreshInBackgroundAsync(string sessionId, CancellationToken cancellationToken);
}
