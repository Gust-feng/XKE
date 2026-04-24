namespace Xke.Core.Schedule;

public sealed class ScheduleException : Exception
{
    public ScheduleException(int statusCode, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Detail { get; }
}
