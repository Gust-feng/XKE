namespace Xke.Core.Course;

public sealed class CourseException : Exception
{
    public CourseException(int statusCode, string detail, string reason = "", object? data = null)
        : base(detail)
    {
        StatusCode = statusCode;
        Detail = detail;
        Reason = reason;
        ErrorData = data;
    }

    public int StatusCode { get; }
    public string Detail { get; }
    public string Reason { get; }
    public object? ErrorData { get; }
}
