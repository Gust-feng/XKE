namespace Xke.Core.Schedule;

public interface IJwCaptchaSolver
{
    Task<JwCaptchaSolveResult> ResolveAsync(
        JwCaptchaSolveContext context,
        CancellationToken cancellationToken = default);
}

public sealed record JwCaptchaSolveContext(
    string ChallengeId,
    string CaptchaImage,
    string ManualCode);

public sealed record JwCaptchaSolveResult(
    bool HasCode,
    string Code,
    string FailureReason,
    bool RequiresManualFallback);

public sealed class ManualCaptchaSolver : IJwCaptchaSolver
{
    public Task<JwCaptchaSolveResult> ResolveAsync(
        JwCaptchaSolveContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var code = (context.ManualCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(new JwCaptchaSolveResult(
                HasCode: false,
                Code: string.Empty,
                FailureReason: "验证码不能为空",
                RequiresManualFallback: true));
        }

        // OCR will implement the same boundary later. If OCR cannot provide a
        // code, the caller must route back here and use the user's manual input.
        return Task.FromResult(new JwCaptchaSolveResult(
            HasCode: true,
            Code: code,
            FailureReason: string.Empty,
            RequiresManualFallback: false));
    }
}
