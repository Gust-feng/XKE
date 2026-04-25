using System.Collections.Concurrent;
using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

public sealed class JwCaptchaChallengeStore
{
    private readonly ConcurrentDictionary<string, StoredJwCaptchaChallenge> _challenges = new(StringComparer.Ordinal);

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

        _challenges[challenge.ChallengeId] = new StoredJwCaptchaChallenge(challenge);
        return challenge;
    }

    public async Task<T> UseAsync<T>(
        string challengeId,
        string sessionId,
        Func<JwCaptchaChallenge, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default,
        DateTimeOffset? now = null)
    {
        var cleanedChallengeId = (challengeId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedChallengeId))
        {
            throw new ScheduleException(400, "验证码 challenge_id 不能为空");
        }

        var current = now ?? DateTimeOffset.Now;
        if (!_challenges.TryGetValue(cleanedChallengeId, out var stored))
        {
            throw new ScheduleException(404, "验证码 challenge 不存在或已过期，请重新刷新课表");
        }

        var challenge = stored.Challenge;
        if (!string.Equals(challenge.SessionId, (sessionId ?? string.Empty).Trim(), StringComparison.Ordinal))
        {
            throw new ScheduleException(403, "验证码 challenge 与当前会话不匹配");
        }

        if (challenge.ExpiresAt <= current)
        {
            _challenges.TryRemove(cleanedChallengeId, out _);
            throw new ScheduleException(410, "验证码已过期，请重新刷新课表");
        }

        await stored.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = now ?? DateTimeOffset.Now;
            if (!_challenges.TryGetValue(cleanedChallengeId, out var currentStored) || !ReferenceEquals(currentStored, stored))
            {
                throw new ScheduleException(409, "验证码 challenge 已被消费，请重新刷新课表");
            }

            if (challenge.ExpiresAt <= current)
            {
                _challenges.TryRemove(cleanedChallengeId, out _);
                throw new ScheduleException(410, "验证码已过期，请重新刷新课表");
            }

            var result = await action(challenge, cancellationToken).ConfigureAwait(false);
            _challenges.TryRemove(cleanedChallengeId, out _);
            return result;
        }
        finally
        {
            stored.Gate.Release();
        }
    }

    private void Cleanup(DateTimeOffset now)
    {
        foreach (var pair in _challenges)
        {
            if (pair.Value.Challenge.ExpiresAt <= now)
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

    private sealed class StoredJwCaptchaChallenge
    {
        public StoredJwCaptchaChallenge(JwCaptchaChallenge challenge)
        {
            Challenge = challenge;
        }

        public JwCaptchaChallenge Challenge { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
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
