namespace Xke.Core.Auth;

public sealed class AuthException : Exception
{
    public AuthException(int statusCode, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public AuthException(int statusCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Detail { get; }
}

public sealed class JwAuthException : Exception
{
    public JwAuthException(string message)
        : base(message)
    {
    }
}
