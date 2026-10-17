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

    public NativeJwScheduleClient(
        TraditionalJwSessionRuntimeStore traditionalSessions,
        XkeDataRoot dataRoot)
        : this(
            new TraditionalJwScheduleHtmlParser(),
            traditionalSessions,
            (user, password, preloginMode) => new TraditionalJwClient(
                user,
                password,
                preloginMode,
                TraditionalJwOptions.FromDataRoot(dataRoot)),
            (user, password) => new DirectJwLoginProtocol(user, password),
            dataRoot)
    {
    }

    public NativeJwScheduleClient(
        TraditionalJwScheduleHtmlParser cookieParser,
        TraditionalJwSessionRuntimeStore traditionalSessions,
        Func<string, string, string, TraditionalJwClient> traditionalClientFactory,
        Func<string, string, DirectJwLoginProtocol>? directLoginFactory = null,
        XkeDataRoot? dataRoot = null)
    {
        _cookieParser = cookieParser;
        _traditionalSessions = traditionalSessions;
        _traditionalClientFactory = traditionalClientFactory;
        _directLoginFactory = directLoginFactory ?? ((user, password) => new DirectJwLoginProtocol(user, password));
        _dataRoot = dataRoot;
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
        throw new TraditionalJwSessionExpiredException("获取学期课表需要先连接教务系统");
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
                _traditionalSessions.Store(runtimeKey, restoredClient);
                _dataRoot.JwSessions().Save(restoredClient.ExportStoredSession());
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

        if (!allowCaptcha) throw new JwScheduleException("教务会话已失效，请打开界面重新连接", 401);
        var loginProtocol = _directLoginFactory(user, password);
        string captchaImage;
        try
        {
            captchaImage = await loginProtocol.BeginAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            loginProtocol.Dispose();
            throw;
        }

        throw new JwCaptchaRequiredException(
            "需要输入教务系统验证码",
            captchaImage,
            expiresInSeconds: 120,
            async (captchaCode, token) =>
            {
                try
                {
                    await loginProtocol.CompleteAsync(captchaCode, token).ConfigureAwait(false);
                    var authenticatedClient = loginProtocol.TransferAuthenticatedSession();
                    try
                    {
                        var result = await authenticatedClient.FetchScheduleAsync(request, _cookieParser, token).ConfigureAwait(false);
                        _traditionalSessions.Store(runtimeKey, authenticatedClient);
                        _dataRoot?.JwSessions().Save(authenticatedClient.ExportStoredSession());
                        return result;
                    }
                    catch
                    {
                        authenticatedClient.Dispose();
                        throw;
                    }
                }
                catch
                {
                    loginProtocol.Dispose();
                    throw;
                }
            });
    }
}
