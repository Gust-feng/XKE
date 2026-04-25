namespace Xke.Core.Schedule;

public class JwScheduleException : Exception
{
    public JwScheduleException(string message, int statusCode = 500)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class TraditionalJwSessionExpiredException : JwScheduleException
{
    public TraditionalJwSessionExpiredException(string message)
        : base(message, statusCode: 401)
    {
    }
}

public sealed class JwCaptchaRequiredException : JwScheduleException
{
    public JwCaptchaRequiredException(
        string message,
        string captchaImage,
        int expiresInSeconds,
        Func<string, CancellationToken, Task<JwScheduleFetchResult>>? continueAsync = null)
        : base(message)
    {
        CaptchaImage = captchaImage;
        ExpiresInSeconds = expiresInSeconds;
        ContinueAsync = continueAsync;
    }

    public string CaptchaImage { get; }
    public int ExpiresInSeconds { get; }

    // The continuation stays server-side because the browser must never own
    // prelogin cookies, request headers, ticket state, or JW bootstrap context.
    public Func<string, CancellationToken, Task<JwScheduleFetchResult>>? ContinueAsync { get; }
}
