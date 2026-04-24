using System.Collections.Concurrent;
using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

public sealed class JwCaptchaChallengeStore
{
    private readonly ConcurrentDictionary<string, JwCaptchaChallenge> _challenges = new(StringComparer.Ordinal);

    public JwCaptchaChallenge Create(
        string sessionId,
        ScheduleRequest request,
        JwCaptchaRequiredException exception,
        DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.Now;
        Cleanup(current);
        var expiresInSeconds = Math.Clamp(exception.ExpiresInSeconds, 30, 300);
        var challenge = new JwCaptchaChallenge(
            ChallengeId: Guid.NewGuid().ToString("N"),
            SessionId: (sessionId ?? string.Empty).Trim(),
            Request: CloneRequest(request),
            CaptchaImage: exception.CaptchaImage,
            ExpiresAt: current.AddSeconds(expiresInSeconds),
            ContinueAsync: exception.ContinueAsync);

        _challenges[challenge.ChallengeId] = challenge;
        return challenge;
    }

    public JwCaptchaChallenge Consume(string challengeId, string sessionId, DateTimeOffset? now = null)
    {
        var cleanedChallengeId = (challengeId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedChallengeId))
        {
            throw new ScheduleException(400, "验证码 challenge_id 不能为空");
        }

        var current = now ?? DateTimeOffset.Now;
        if (!_challenges.TryGetValue(cleanedChallengeId, out var challenge))
        {
            throw new ScheduleException(404, "验证码 challenge 不存在或已过期，请重新刷新课表");
        }

        if (!string.Equals(challenge.SessionId, (sessionId ?? string.Empty).Trim(), StringComparison.Ordinal))
        {
            throw new ScheduleException(403, "验证码 challenge 与当前会话不匹配");
        }

        if (challenge.ExpiresAt <= current)
        {
            _challenges.TryRemove(cleanedChallengeId, out _);
            throw new ScheduleException(410, "验证码已过期，请重新刷新课表");
        }

        if (!_challenges.TryRemove(cleanedChallengeId, out var removed))
        {
            throw new ScheduleException(409, "验证码 challenge 已被消费，请重新刷新课表");
        }

        return removed;
    }

    private void Cleanup(DateTimeOffset now)
    {
        foreach (var pair in _challenges)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _challenges.TryRemove(pair.Key, out _);
            }
        }
    }

    private static ScheduleRequest CloneRequest(ScheduleRequest request)
    {
        return new ScheduleRequest
        {
            Mode = request.Mode,
            ManualWeek = request.ManualWeek,
            CookieRq = request.CookieRq,
            TermLabel = request.TermLabel,
            AcademicTerm = request.AcademicTerm,
        };
    }
}

public sealed record JwCaptchaChallenge(
    string ChallengeId,
    string SessionId,
    ScheduleRequest Request,
    string CaptchaImage,
    DateTimeOffset ExpiresAt,
    Func<string, CancellationToken, Task<JwScheduleFetchResult>>? ContinueAsync)
{
    public int RemainingSeconds(DateTimeOffset now)
    {
        return Math.Max(0, (int)Math.Ceiling((ExpiresAt - now).TotalSeconds));
    }
}
