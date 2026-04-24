namespace Xke.Core.Feedback;

public sealed class FeedbackException : Exception
{
    public FeedbackException(int statusCode, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Detail { get; }
}
