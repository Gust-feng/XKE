namespace Xke.Core.Schedule;

public class JwScheduleException : Exception
{
    public JwScheduleException(string message)
        : base(message)
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
