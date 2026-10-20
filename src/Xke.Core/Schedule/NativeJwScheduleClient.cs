using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Storage;

namespace Xke.Core.Schedule;

public sealed class NativeJwScheduleClient : IJwScheduleClient
{
    private readonly TraditionalJwScheduleHtmlParser _cookieParser;
    private readonly TraditionalJwSessionRuntimeStore _traditionalSessions;
    private readonly Func<string, string, string, TraditionalJwClient> _traditionalClientFactory;
    private readonly Func<string, string, DirectJwLoginProtocol> _directLoginFactory;
    private readonly XkeDataRoot? _dataRoot;
    private readonly AutomaticCaptchaLogin? _automatic;

    public NativeJwScheduleClient(
        TraditionalJwSessionRuntimeStore traditionalSessions,
        XkeDataRoot dataRoot, AutomaticCaptchaLogin? automatic = null,
        Func<string, string, DirectJwLoginProtocol>? directLoginFactory = null)
        : this(
            new TraditionalJwScheduleHtmlParser(),
            traditionalSessions,
            (user, password, preloginMode) => new TraditionalJwClient(
                user,
                password,
                preloginMode,
                TraditionalJwOptions.FromDataRoot(dataRoot)),
            directLoginFactory ?? ((user, password) => new DirectJwLoginProtocol(user, password)),
            dataRoot, automatic)
    {
    }

    public NativeJwScheduleClient(
        TraditionalJwScheduleHtmlParser cookieParser,
        TraditionalJwSessionRuntimeStore traditionalSessions,
        Func<string, string, string, TraditionalJwClient> traditionalClientFactory,
        Func<string, string, DirectJwLoginProtocol>? directLoginFactory = null,
        XkeDataRoot? dataRoot = null, AutomaticCaptchaLogin? automatic = null)
    {
        _cookieParser = cookieParser;
        _traditionalSessions = traditionalSessions;
        _traditionalClientFactory = traditionalClientFactory;
        _directLoginFactory = directLoginFactory ?? ((user, password) => new DirectJwLoginProtocol(user, password));
        _dataRoot = dataRoot;
        _automatic = automatic;
    }

    public Task<JwScheduleFetchResult> FetchCookieAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string sessionId = "")
    {
        return FetchCookieFromChallengeAsync(user, password, preloginMode, request, sessionId, cancellationToken);
    }

    public Task<JwScheduleFetchResult> FetchExistingSessionAsync(
        string user, string password, string preloginMode, ScheduleRequest request, string sessionId, CancellationToken cancellationToken,
        Action? validateContinuation = null)
    {
        return FetchCookieFromChallengeAsync(user, password, preloginMode, request, sessionId, cancellationToken,
            allowCaptcha: false, validateContinuation: validateContinuation);
    }

    public async Task<JwScheduleFetchResult> FetchSemesterAsync(string user, string password, string preloginMode,
        string academicTerm, string sessionId, CancellationToken cancellationToken = default)
    {
        var runtimeKey = string.IsNullOrWhiteSpace(sessionId) ? user : sessionId.Trim();
        if (_traditionalSessions.TryGet(runtimeKey, out var client))
        {
            try { return await client.FetchSemesterScheduleAsync(academicTerm, _cookieParser, cancellationToken).ConfigureAwait(false); }
            catch (TraditionalJwSessionExpiredException) { _traditionalSessions.Remove(runtimeKey); }
        }
        if (_dataRoot?.JwSessions().Load(user) is { Cookies.Count: > 0 } stored)
        {
            var restored = _traditionalClientFactory(user, password, preloginMode);
            restored.ImportStoredSession(stored);
            try
            {
                var result = await restored.FetchSemesterScheduleAsync(academicTerm, _cookieParser, cancellationToken).ConfigureAwait(false);
                _traditionalSessions.Store(runtimeKey, restored);
                return result;
            }
            catch (TraditionalJwSessionExpiredException)
            {
                restored.Dispose();
                _dataRoot.JwSessions().Remove(user);
            }
            catch { restored.Dispose(); throw; }
        }
        return await FetchWithLoginAsync(user, password, runtimeKey,
            (client, token) => client.FetchSemesterScheduleAsync(academicTerm, _cookieParser, token),
            allowManual: false, cancellationToken).ConfigureAwait(false);
    }
    private async Task<JwScheduleFetchResult> FetchCookieFromChallengeAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        string sessionId,
        CancellationToken cancellationToken,
        bool allowCaptcha = true,
        Action? validateContinuation = null)
    {
        validateContinuation?.Invoke();
        var runtimeKey = string.IsNullOrWhiteSpace(sessionId) ? user : sessionId.Trim();
        if (_traditionalSessions.TryGet(runtimeKey, out var existingClient))
        {
            try
            {
                var result = await existingClient.FetchScheduleAsync(request, _cookieParser, cancellationToken).ConfigureAwait(false);
                validateContinuation?.Invoke();
                _dataRoot?.JwSessions().Save(existingClient.ExportStoredSession());
                return result;
            }
            catch (TraditionalJwSessionExpiredException)
            {
                validateContinuation?.Invoke();
                _traditionalSessions.Remove(runtimeKey);
            }
        }

        validateContinuation?.Invoke();
        if (_dataRoot?.JwSessions().Load(user) is { Cookies.Count: > 0 } persisted)
        {
            var restoredClient = _traditionalClientFactory(user, password, preloginMode);
            restoredClient.ImportStoredSession(persisted);
            try
            {
                var result = await restoredClient.FetchScheduleAsync(request, _cookieParser, cancellationToken).ConfigureAwait(false);
                validateContinuation?.Invoke();
                _dataRoot.JwSessions().Save(restoredClient.ExportStoredSession());
                _traditionalSessions.Store(runtimeKey, restoredClient);
                return result;
            }
            catch (TraditionalJwSessionExpiredException)
            {
                restoredClient.Dispose();
                validateContinuation?.Invoke();
                _dataRoot.JwSessions().Remove(user);
            }
            catch
            {
                restoredClient.Dispose();
                validateContinuation?.Invoke();
                throw;
            }
        }

        return await FetchWithLoginAsync(user, password, runtimeKey,
            (client, token) => client.FetchScheduleAsync(request, _cookieParser, token),
            allowCaptcha, cancellationToken, validateContinuation).ConfigureAwait(false);
    }

    private async Task<JwScheduleFetchResult> FetchWithLoginAsync(string user, string password, string runtimeKey,
        Func<TraditionalJwClient, CancellationToken, Task<JwScheduleFetchResult>> fetch, bool allowManual,
        CancellationToken cancellationToken, Action? validate = null)
    {
        validate?.Invoke();
        if (!allowManual && _automatic?.CanUse(user, password) != true)
            throw new JwScheduleException("教务会话已失效，请打开界面手动验证", 401);
        if (validate is null && _dataRoot is not null && runtimeKey != user)
            validate = () => { if (_dataRoot.Sessions().Load(runtimeKey) is null) throw new JwScheduleException("登录状态已变更，本次认证已取消", 409); };
        validate?.Invoke();
        var protocol = _directLoginFactory(user, password);
        string image;
        try
        {
            image = await protocol.BeginAsync(cancellationToken).ConfigureAwait(false);
            if (_automatic?.CanUse(user, password) == true)
            {
                if (await _automatic.TryLoginAsync(user, password, protocol, image, cancellationToken, validate).ConfigureAwait(false))
                {
                    using (protocol) return await FetchAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
                }
                if (!allowManual) throw new JwScheduleException("自动认证未通过，请打开界面手动验证", 401);
                image = await protocol.BeginAsync(cancellationToken).ConfigureAwait(false);
            }
            validate?.Invoke();
        }
        catch { protocol.Dispose(); throw; }

        throw new JwCaptchaRequiredException(_automatic?.GetStatus(user).Message is { Length: > 0 } message ? message : "需要输入教务系统验证码", image, 120, async (code, token) =>
        {
            using (protocol)
            {
                try { await protocol.CompleteAsync(code, token).ConfigureAwait(false); }
                catch (JwLoginException error) when (error.Kind == JwLoginFailureKind.CaptchaRejected)
                {
                    _automatic?.RequireManual(user, error.Message);
                    throw;
                }
                var result = await FetchAuthenticatedAsync(token).ConfigureAwait(false);
                _automatic?.Reset(user);
                return result;
            }
        });

        async Task<JwScheduleFetchResult> FetchAuthenticatedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            validate?.Invoke();
            var authenticated = protocol.TransferAuthenticatedSession();
            try
            {
                var result = await fetch(authenticated, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                validate?.Invoke();
                _dataRoot?.JwSessions().Save(authenticated.ExportStoredSession());
                _traditionalSessions.Store(runtimeKey, authenticated);
                return result;
            }
            catch { authenticated.Dispose(); throw; }
        }
    }
}
