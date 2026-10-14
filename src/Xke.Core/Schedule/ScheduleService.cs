using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth.Jw;
using Xke.Storage;

namespace Xke.Core.Schedule;

public sealed class ScheduleService : IScheduleRefresh, IDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly XkeDataRoot _dataRoot;
    private readonly IJwScheduleClient _scheduleClient;
    private readonly IScheduleSnapshotSource? _snapshotSource;
    private readonly MobileSessionRuntimeStore _mobileSessionRuntime;
    private readonly JwCaptchaChallengeStore _captchaChallengeStore;
    private readonly IJwCaptchaSolver _captchaSolver;
    private readonly bool _useLegacyMobileSchedule;
    private readonly Func<string?>? _activeSession;

    public ScheduleService(
        XkeDataRoot dataRoot,
        IJwScheduleClient scheduleClient,
        MobileSessionRuntimeStore mobileSessionRuntime,
        JwCaptchaChallengeStore captchaChallengeStore,
        IJwCaptchaSolver captchaSolver,
        bool useLegacyMobileSchedule = false,
        IScheduleSnapshotSource? snapshotSource = null,
        Func<string?>? activeSession = null)
    {
        _dataRoot = dataRoot;
        _scheduleClient = scheduleClient;
        _snapshotSource = snapshotSource;
        _activeSession = activeSession;
        _mobileSessionRuntime = mobileSessionRuntime;
        _captchaChallengeStore = captchaChallengeStore;
        _captchaSolver = captchaSolver;
        _useLegacyMobileSchedule = useLegacyMobileSchedule;
    }

    public async Task<(string Message, SemesterScheduleData Payload)> FetchSemesterAsync(string sessionId, string academicTerm,
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var context = LoadContext(sessionId);
            var snapshot = _snapshotSource is null
                ? ScheduleSnapshotAttempt.Unavailable(string.Empty)
                : await _snapshotSource.FetchSemesterAsync(context.Profile.StudentId, academicTerm, cancellationToken).ConfigureAwait(false);
            JwScheduleFetchResult result;
            try
            {
                result = snapshot.Schedule ?? await _scheduleClient.FetchSemesterAsync(context.Session.User, context.Credentials.Password,
                    context.Session.PreloginMode, academicTerm, sessionId, cancellationToken).ConfigureAwait(false);
            }
            catch (JwScheduleException ex) { throw new ScheduleException(ex.StatusCode, ex.Message); }
            var payload = ScheduleDataMapper.FromSemester(result, context.Profile);
            cancellationToken.ThrowIfCancellationRequested();
            payload = context.ScheduleStore.SaveSemesterSchedule(payload);
            return ("学期课表获取成功", payload);
        }
        finally { _refreshGate.Release(); }
    }

    public (string Message, SemesterScheduleData Payload) LoadLocalSemester(string sessionId, string academicTerm)
    {
        var context = LoadContext(sessionId);
        var payload = context.ScheduleStore.LoadSemesterSchedule(academicTerm)
            ?? throw new ScheduleException(404, "当前账号暂无匹配学期的本地课表");
        return ("本地学期课表读取成功", payload);
    }

    public async Task RefreshInBackgroundAsync(string sessionId, CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await RefreshBackgroundCoreAsync(sessionId, cancellationToken).ConfigureAwait(false); }
        finally { _refreshGate.Release(); }
    }

    public void Dispose() => _refreshGate.Dispose();

    private async Task RefreshBackgroundCoreAsync(string sessionId, CancellationToken cancellationToken)
    {
        var context = LoadContext(sessionId);
        var settings = context.StudentStore.LoadPreferences()?.Schedule ?? new ScheduleSettingsDocument();
        var revision = ScheduleRefreshScheduler.ConfigurationKey(sessionId, settings);
        void Validate()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = _dataRoot.Sessions().Load(sessionId);
            var current = context.StudentStore.LoadPreferences()?.Schedule;
            if (session is null || session.StudentId != context.Session.StudentId || session.User != context.Session.User
                || _activeSession is not null && _activeSession() != sessionId
                || current?.AutoRefreshEnabled != true
                || ScheduleRefreshScheduler.ConfigurationKey(sessionId, current) != revision)
                throw new ScheduleException(409, "账号或自动刷新设置已变更，本次刷新已取消");
        }
        Validate();
        var request = new ScheduleRequest { Mode = "current", AcademicTerm = settings.AcademicTerm };
        ScheduleSnapshotAttempt snapshot;
        try { snapshot = await TrySnapshotAsync(context, request, cancellationToken).ConfigureAwait(false); }
        catch { Validate(); throw; }
        Validate();
        if (snapshot.Schedule is { } schedule)
        {
            SaveLivePayload(context, schedule, request, string.Empty);
            return;
        }
        try
        {
            if (_scheduleClient is not NativeJwScheduleClient native)
                throw new ScheduleException(503, "当前课表客户端未提供后台刷新");
            var result = await native.FetchExistingSessionAsync(
                context.Session.User, context.Credentials.Password, context.Session.PreloginMode, request, sessionId, cancellationToken,
                validateContinuation: Validate).ConfigureAwait(false);
            Validate();
            SaveLivePayload(context, result, request, snapshot.FallbackReason);
        }
        catch (JwScheduleException ex) { Validate(); throw new ScheduleException(ex.StatusCode, ex.Message); }
        catch { Validate(); throw; }
    }

    private Task<ScheduleSnapshotAttempt> TrySnapshotAsync(ScheduleContext context, ScheduleRequest request, CancellationToken cancellationToken)
    {
        return _snapshotSource?.FetchAsync(context.Profile.StudentId, request, cancellationToken)
               ?? Task.FromResult(ScheduleSnapshotAttempt.Unavailable(string.Empty));
    }

    public (string Message, Dictionary<string, object?> Payload) LoadLocal(string sessionId, string weekLabel, string source, string academicTerm = "")
    {
        var context = LoadContext(sessionId);
        var sourceKeys = XkeScheduleStore.ResolveSourceKeys(source);
        Dictionary<string, object?>? cached;
        if (!string.IsNullOrWhiteSpace(weekLabel))
        {
            cached = context.ScheduleStore.LoadScheduleForWeek(weekLabel, sourceKeys, academicTerm);
        }
        else
        {
            var inferredCurrentWeek = context.ScheduleStore.FindScheduleWeekLabelForDate(null, sourceKeys, academicTerm);
            cached = !string.IsNullOrWhiteSpace(inferredCurrentWeek)
                ? context.ScheduleStore.LoadScheduleForWeek(inferredCurrentWeek, sourceKeys, academicTerm)
                : null;
            cached ??= context.ScheduleStore.LoadLatestSourceSchedule(sourceKeys, academicTerm);
        }

        if (cached is null)
        {
            throw new ScheduleException(404, "当前账号暂无匹配的本地课表缓存，请使用密码登录重新完成初始化");
        }

        var payload = NormalizeSchedulePayload(
            cached,
            context,
            cached: true,
            dataOrigin: "local_cache",
            sourceKey: cached.GetValueOrDefault("source_key")?.ToString() ?? string.Empty,
            sourceLabel: cached.GetValueOrDefault("source_label")?.ToString() ?? cached.GetValueOrDefault("source")?.ToString() ?? "课表数据",
            sourceType: cached.GetValueOrDefault("source_type")?.ToString() ?? string.Empty,
            fallbackReason: string.Empty,
            request: null);

        return ("本地课表已加载", payload);
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> FetchPreferredAsync(
        string sessionId,
        ScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_useLegacyMobileSchedule)
        {
            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var preferredContext = LoadContext(sessionId);
                var snapshot = await TrySnapshotAsync(preferredContext, request, cancellationToken).ConfigureAwait(false);
                if (snapshot.Schedule is { } schedule)
                    return ("课表已更新", SaveLivePayload(preferredContext, schedule, request, string.Empty));
                return await FetchCookieCoreAsync(sessionId, request, cancellationToken, snapshot.FallbackReason).ConfigureAwait(false);
            }
            finally { _refreshGate.Release(); }
        }

        var context = LoadContext(sessionId);
        try
        {
            _mobileSessionRuntime.TryGetToken(sessionId, out var existingToken);
            var result = await _scheduleClient.FetchPreferredAsync(
                context.Session.User,
                context.Credentials.Password,
                context.Session.PreloginMode,
                request,
                cancellationToken,
                existingToken,
                context.Credentials.MobileJw).ConfigureAwait(false);
            SaveMobileJwCredentialIfNeeded(context, result.MobileJw);
            var payload = SaveLivePayload(context, result, request, fallbackReason: string.Empty);
            return ("课表获取成功", payload);
        }
        catch (JwScheduleException ex)
        {
            throw new ScheduleException(ex.StatusCode, ex.Message);
        }
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> FetchCookieAsync(
        string sessionId, ScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await FetchCookieCoreAsync(sessionId, request, cancellationToken).ConfigureAwait(false); }
        finally { _refreshGate.Release(); }
    }

    private async Task<(string Message, Dictionary<string, object?> Payload)> FetchCookieCoreAsync(
        string sessionId,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string fallbackReason = "")
    {
        var context = LoadContext(sessionId);
        try
        {
            var result = await _scheduleClient.FetchCookieAsync(
                context.Session.User,
                context.Credentials.Password,
                context.Session.PreloginMode,
                request,
                cancellationToken,
                existingToken: string.Empty,
                mobileJw: context.Credentials.MobileJw,
                sessionId: context.SessionId).ConfigureAwait(false);
            var payload = SaveLivePayload(context, result, request, fallbackReason: fallbackReason);
            return ("Cookie 课表获取成功", payload);
        }
        catch (JwCaptchaRequiredException ex)
        {
            var challenge = _captchaChallengeStore.Create(context.SessionId, request, ex);
            return ("需要输入教务系统验证码", BuildCaptchaPayload(challenge));
        }
        catch (JwScheduleException ex)
        {
            throw new ScheduleException(ex.StatusCode, ex.Message);
        }
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> SubmitJwCaptchaAsync(
        string sessionId,
        JwCaptchaSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ScheduleException(400, "验证码提交参数不能为空");
        }

        var context = LoadContext(sessionId);
        return await _captchaChallengeStore.UseAsync(
            request.ChallengeId,
            context.SessionId,
            async (challenge, token) =>
            {
                var solved = await _captchaSolver.ResolveAsync(
                    new JwCaptchaSolveContext(
                        challenge.ChallengeId,
                        challenge.CaptchaImage,
                        request.CaptchaCode),
                    token).ConfigureAwait(false);
                if (!solved.HasCode)
                {
                    throw new ScheduleException(400, solved.FailureReason);
                }

                if (challenge.ContinueAsync is null)
                {
                    throw new ScheduleException(409, "验证码已提交，但真实教务登录续跑尚未接入；请使用开发态 HTML fixture 或等待下一切片");
                }

                try
                {
                    var result = await challenge.ContinueAsync(solved.Code, token).ConfigureAwait(false);
                    var payload = SaveLivePayload(context, result, challenge.Request, fallbackReason: string.Empty);
                    return ("Cookie 课表获取成功", payload);
                }
                catch (JwScheduleException ex)
                {
                    throw new ScheduleException(ex.StatusCode, ex.Message);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildCaptchaPayload(JwCaptchaChallenge challenge)
    {
        var challengePayload = new JwCaptchaChallengePayload(
            Status: "captcha_required",
            ChallengeId: challenge.ChallengeId,
            CaptchaImage: challenge.CaptchaImage,
            ExpiresInSeconds: challenge.RemainingSeconds(DateTimeOffset.Now));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = challengePayload.Status,
            ["challenge_id"] = challengePayload.ChallengeId,
            ["captcha_image"] = challengePayload.CaptchaImage,
            ["expires_in_seconds"] = challengePayload.ExpiresInSeconds,
        };
    }

    private Dictionary<string, object?> SaveLivePayload(
        ScheduleContext context,
        JwScheduleFetchResult result,
        ScheduleRequest request,
        string fallbackReason)
    {
        if (DateTimeOffset.TryParse(result.RawPayload.GetValueOrDefault("updated_at")?.ToString(), out var incomingDate))
        {
            var existing = context.ScheduleStore.LoadScheduleForWeek(result.WeekLabel, ["jw_schedule"], (result.RawPayload.GetValueOrDefault("term") as Dictionary<string, object?>)?.GetValueOrDefault("academic_term")?.ToString() ?? "");
            if (existing is not null && DateTimeOffset.TryParse(existing.GetValueOrDefault("updated_at")?.ToString(), out var existingDate)
                && existingDate > incomingDate && SameTerm(existing, result.RawPayload))
            {
                return NormalizeSchedulePayload(existing, context, true, "local_cache", "jw_schedule", "教务系统课表", "jw", string.Empty, request);
            }
        }
        var payload = ScheduleDataMapper.FromWeek(result, context.Profile, request, fallbackReason);
        RestoreCalendar(payload, context);

        context.ScheduleStore.SaveSourceSchedule(
            payload,
            result.RawPayload,
            payload.GetValueOrDefault("source_key")?.ToString() ?? result.SourceKey,
            payload.GetValueOrDefault("week_label")?.ToString() ?? result.WeekLabel);
        return payload;
    }

    private static bool SameTerm(Dictionary<string, object?> existing, Dictionary<string, object?> incoming)
    {
        var previous = existing.GetValueOrDefault("term") as Dictionary<string, object?>;
        var next = incoming.GetValueOrDefault("term") as Dictionary<string, object?>;
        return string.Equals(previous?.GetValueOrDefault("academic_term")?.ToString() ?? string.Empty,
            next?.GetValueOrDefault("academic_term")?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    private Dictionary<string, object?> NormalizeSchedulePayload(
        Dictionary<string, object?> rawPayload, ScheduleContext context, bool cached, string dataOrigin,
        string sourceKey, string sourceLabel, string sourceType, string fallbackReason, ScheduleRequest? request)
    {
        var payload = ScheduleDataMapper.NormalizeWeek(rawPayload, context.Profile, cached, dataOrigin,
            sourceKey, sourceLabel, sourceType, fallbackReason, request);
        RestoreCalendar(payload, context);
        if (cached && payload.GetValueOrDefault("_file") is string sourceFile && sourceFile.Length > 0)
            payload["saved_files"] = new List<object?> { _dataRoot.RelativePath(sourceFile) };
        payload.Remove("_file");
        payload.Remove("_file_updated_at");
        return payload;
    }

    private static void RestoreCalendar(Dictionary<string, object?> payload, ScheduleContext context)
    {
        if (payload.GetValueOrDefault("week_dates") is Dictionary<string, object?> dates && dates.Count > 0) return;
        var term = (payload.GetValueOrDefault("term") as Dictionary<string, object?>)?.GetValueOrDefault("academic_term")?.ToString() ?? string.Empty;
        var week = ScheduleDataMapper.ParseWeekNumber(payload.GetValueOrDefault("week_label")?.ToString());
        if (week is not > 0 || context.ScheduleStore.FindSemesterStartDate(term, ["jw_schedule"]) is not { } start) return;
        string[] weekdays = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
        payload["week_dates"] = weekdays.Select((day, index) => new { day, date = start.AddDays((week.Value - 1) * 7 + index) })
            .ToDictionary(item => item.day, item => (object?)item.date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
    }

    private void SaveMobileJwCredentialIfNeeded(ScheduleContext context, MobileJwEncryptedCredentialDocument? mobileJw)
    {
        if (mobileJw is null || string.IsNullOrWhiteSpace(mobileJw.EncryptedPassword))
        {
            return;
        }

        _dataRoot.Credentials().Save(new StoredCredentialDocument
        {
            User = context.Credentials.User,
            Password = context.Credentials.Password,
            StudentId = context.Credentials.StudentId,
            TrustedLoginToken = context.Credentials.TrustedLoginToken,
            MobileJw = mobileJw,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
    }

    private ScheduleContext LoadContext(string sessionId)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            throw new ScheduleException(400, "会话不能为空");
        }

        var session = _dataRoot.Sessions().Load(cleanedSessionId)
            ?? throw new ScheduleException(401, "会话不存在或已过期，请重新登录");
        var credentials = _dataRoot.Credentials().Load(session.User)
            ?? throw new ScheduleException(401, "本地凭据不存在，请重新登录");
        var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
        var studentStore = _dataRoot.ForStudent(studentId);
        var profile = studentStore.LoadProfile() ?? new StudentProfileDocument
        {
            StudentId = studentId,
            Name = studentId,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };

        return new ScheduleContext(
            SessionId: cleanedSessionId,
            Session: session,
            Credentials: credentials,
            StudentStore: studentStore,
            ScheduleStore: studentStore.Schedules(),
            Profile: profile);
    }

    public static int? ParseWeekNumber(string? weekLabel) => ScheduleDataMapper.ParseWeekNumber(weekLabel);

    private sealed record ScheduleContext(
        string SessionId,
        StoredSessionDocument Session,
        StoredCredentialDocument Credentials,
        XkeStudentStore StudentStore,
        XkeScheduleStore ScheduleStore,
        StudentProfileDocument Profile);
}
