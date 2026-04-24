using System.Text.RegularExpressions;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth.Jw;
using Xke.Storage;

namespace Xke.Core.Schedule;

public sealed class ScheduleService
{
    private readonly XkeDataRoot _dataRoot;
    private readonly IJwScheduleClient _scheduleClient;
    private readonly MobileSessionRuntimeStore _mobileSessionRuntime;
    private readonly JwCaptchaChallengeStore _captchaChallengeStore;
    private readonly IJwCaptchaSolver _captchaSolver;

    public ScheduleService(
        XkeDataRoot dataRoot,
        IJwScheduleClient scheduleClient,
        MobileSessionRuntimeStore mobileSessionRuntime,
        JwCaptchaChallengeStore captchaChallengeStore,
        IJwCaptchaSolver captchaSolver)
    {
        _dataRoot = dataRoot;
        _scheduleClient = scheduleClient;
        _mobileSessionRuntime = mobileSessionRuntime;
        _captchaChallengeStore = captchaChallengeStore;
        _captchaSolver = captchaSolver;
    }

    public (string Message, Dictionary<string, object?> Payload) LoadLocal(string sessionId, string weekLabel, string source)
    {
        var context = LoadContext(sessionId);
        var sourceKeys = XkeScheduleStore.ResolveSourceKeys(source);
        Dictionary<string, object?>? cached;
        if (!string.IsNullOrWhiteSpace(weekLabel))
        {
            cached = context.ScheduleStore.LoadScheduleForWeek(weekLabel, sourceKeys);
        }
        else
        {
            var inferredCurrentWeek = context.ScheduleStore.FindScheduleWeekLabelForDate(null, sourceKeys);
            cached = !string.IsNullOrWhiteSpace(inferredCurrentWeek)
                ? context.ScheduleStore.LoadScheduleForWeek(inferredCurrentWeek, sourceKeys)
                : null;
            cached ??= context.ScheduleStore.LoadLatestSourceSchedule(sourceKeys);
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
            throw new ScheduleException(500, ex.Message);
        }
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> FetchCookieAsync(
        string sessionId,
        ScheduleRequest request,
        CancellationToken cancellationToken = default)
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
                mobileJw: context.Credentials.MobileJw).ConfigureAwait(false);
            var payload = SaveLivePayload(context, result, request, fallbackReason: string.Empty);
            return ("Cookie 课表获取成功", payload);
        }
        catch (JwCaptchaRequiredException ex)
        {
            var challenge = _captchaChallengeStore.Create(context.SessionId, request, ex);
            return ("需要输入教务系统验证码", BuildCaptchaPayload(challenge));
        }
        catch (JwScheduleException ex)
        {
            throw new ScheduleException(500, ex.Message);
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
        var challenge = _captchaChallengeStore.Consume(request.ChallengeId, context.SessionId);
        var solved = await _captchaSolver.ResolveAsync(
            new JwCaptchaSolveContext(
                challenge.ChallengeId,
                challenge.CaptchaImage,
                request.CaptchaCode),
            cancellationToken).ConfigureAwait(false);
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
            var result = await challenge.ContinueAsync(solved.Code, cancellationToken).ConfigureAwait(false);
            var payload = SaveLivePayload(context, result, challenge.Request, fallbackReason: string.Empty);
            return ("Cookie 课表获取成功", payload);
        }
        catch (JwScheduleException ex)
        {
            throw new ScheduleException(400, ex.Message);
        }
    }

    private static Dictionary<string, object?> BuildCaptchaPayload(JwCaptchaChallenge challenge)
    {
        var payload = new JwCaptchaChallengePayload(
            Status: "captcha_required",
            ChallengeId: challenge.ChallengeId,
            CaptchaImage: challenge.CaptchaImage,
            ExpiresInSeconds: challenge.RemainingSeconds(DateTimeOffset.Now));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = payload.Status,
            ["challenge_id"] = payload.ChallengeId,
            ["captcha_image"] = payload.CaptchaImage,
            ["expires_in_seconds"] = payload.ExpiresInSeconds,
        };
    }

    private Dictionary<string, object?> SaveLivePayload(
        ScheduleContext context,
        JwScheduleFetchResult result,
        ScheduleRequest request,
        string fallbackReason)
    {
        var payload = BuildPayloadFromFetchResult(result);
        payload = NormalizeSchedulePayload(
            payload,
            context,
            cached: false,
            dataOrigin: "live_fetch",
            sourceKey: result.SourceKey,
            sourceLabel: result.SourceLabel,
            sourceType: result.SourceType,
            fallbackReason: fallbackReason,
            request: request);

        context.ScheduleStore.SaveSourceSchedule(
            payload,
            result.RawPayload,
            payload.GetValueOrDefault("source_key")?.ToString() ?? result.SourceKey,
            payload.GetValueOrDefault("week_label")?.ToString() ?? result.WeekLabel);
        return payload;
    }

    private static Dictionary<string, object?> BuildPayloadFromFetchResult(JwScheduleFetchResult result)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = result.SourceLabel,
            ["source_label"] = result.SourceLabel,
            ["source_key"] = result.SourceKey,
            ["source_type"] = result.SourceType,
            ["rows"] = result.Rows.Cast<object?>().ToList(),
            ["week_label"] = result.WeekLabel,
            ["available_weeks"] = result.AvailableWeeks.Cast<object?>().ToList(),
            ["week_dates"] = result.WeekDates.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal),
            ["current_week_number"] = result.CurrentWeekNumber,
            ["viewed_week_number"] = result.ViewedWeekNumber,
            ["remarks"] = result.Remarks,
            ["block_times"] = result.BlockTimes.Cast<object?>().ToList(),
            ["fallback_reason"] = result.FallbackReason,
            ["data_warnings"] = result.DataWarnings.Cast<object?>().ToList(),
        };
    }

    private Dictionary<string, object?> NormalizeSchedulePayload(
        Dictionary<string, object?> rawPayload,
        ScheduleContext context,
        bool cached,
        string dataOrigin,
        string sourceKey,
        string sourceLabel,
        string sourceType,
        string fallbackReason,
        ScheduleRequest? request)
    {
        var payload = new Dictionary<string, object?>(rawPayload, StringComparer.Ordinal);
        var resolvedSourceKey = XkeScheduleStore.NormalizeSourceKey(sourceKey);
        var resolvedSourceType = NormalizeSourceType(sourceType, resolvedSourceKey);
        var resolvedSourceLabel = string.IsNullOrWhiteSpace(sourceLabel)
            ? ResolveSourceLabel(resolvedSourceKey)
            : sourceLabel.Trim();
        var weekLabel = (payload.GetValueOrDefault("week_label")?.ToString() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(weekLabel))
        {
            weekLabel = "当前周";
        }

        var currentWeekLabel = (payload.GetValueOrDefault("current_week_label")?.ToString() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(currentWeekLabel))
        {
            currentWeekLabel = weekLabel;
        }

        payload["source"] = resolvedSourceLabel;
        payload["source_label"] = resolvedSourceLabel;
        payload["source_key"] = resolvedSourceKey;
        payload["source_type"] = resolvedSourceType;
        payload["rows"] = NormalizeList(payload.GetValueOrDefault("rows"));
        payload["week_label"] = weekLabel;
        payload["available_weeks"] = NormalizeAvailableWeeks(payload.GetValueOrDefault("available_weeks"), weekLabel, currentWeekLabel);
        payload["week_dates"] = NormalizeStringDictionary(payload.GetValueOrDefault("week_dates"));
        payload["current_week_label"] = currentWeekLabel;
        payload["current_week_number"] = ParseWeekNumber(currentWeekLabel);
        payload["viewed_week_number"] = ParseWeekNumber(weekLabel);
        payload["remarks"] = (payload.GetValueOrDefault("remarks")?.ToString() ?? (cached ? "当前显示本地缓存课表" : "课表刷新成功")).Trim();
        payload["block_times"] = NormalizeList(payload.GetValueOrDefault("block_times"));
        payload["fallback_reason"] = string.IsNullOrWhiteSpace(fallbackReason)
            ? (payload.GetValueOrDefault("fallback_reason")?.ToString() ?? string.Empty)
            : fallbackReason;
        payload["data_warnings"] = NormalizeList(payload.GetValueOrDefault("data_warnings"));
        payload["term"] = NormalizeTerm(payload.GetValueOrDefault("term"), request);
        payload["student"] = BuildStudentPayload(context.Profile);
        payload["cached"] = cached;
        payload["data_origin"] = dataOrigin;
        payload["updated_at"] = NormalizeTimestamp(payload.GetValueOrDefault("updated_at"));
        payload["file_updated_at"] = payload.GetValueOrDefault("_file_updated_at")?.ToString()
            ?? payload.GetValueOrDefault("file_updated_at")?.ToString()
            ?? string.Empty;

        if (cached)
        {
            var savedFiles = NormalizeList(payload.GetValueOrDefault("saved_files"));
            var sourceFile = payload.GetValueOrDefault("_file")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceFile))
            {
                savedFiles = new List<object?> { _dataRoot.RelativePath(sourceFile) };
            }

            payload["saved_files"] = savedFiles;
        }
        else if (!payload.ContainsKey("saved_files"))
        {
            payload["saved_files"] = new List<object?>();
        }

        payload.Remove("_file");
        payload.Remove("_file_updated_at");
        return payload;
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

    private static Dictionary<string, object?> BuildStudentPayload(StudentProfileDocument profile)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["student_id"] = profile.StudentId,
            ["name"] = string.IsNullOrWhiteSpace(profile.Name) ? profile.StudentId : profile.Name,
            ["class_name"] = profile.ClassName,
            ["major_name"] = profile.MajorName,
            ["academy_name"] = profile.AcademyName,
            ["training_level"] = profile.TrainingLevel,
            ["campus_name"] = profile.CampusName,
        };
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

    private static List<object?> NormalizeList(object? value)
    {
        return value switch
        {
            null => [],
            List<object?> list => list,
            IEnumerable<object?> values => values.ToList(),
            System.Collections.IEnumerable values => values.Cast<object?>().ToList(),
            _ => [],
        };
    }

    private static Dictionary<string, object?> NormalizeStringDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static List<object?> NormalizeAvailableWeeks(object? value, string weekLabel, string currentWeekLabel)
    {
        var weeks = NormalizeList(value);
        if (weeks.Count == 0 && !string.IsNullOrWhiteSpace(weekLabel))
        {
            weeks.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = ParseWeekNumber(weekLabel)?.ToString() ?? weekLabel,
                ["label"] = weekLabel,
                ["selected"] = string.Equals(weekLabel, currentWeekLabel, StringComparison.Ordinal),
            });
        }

        return weeks;
    }

    private static Dictionary<string, object?> NormalizeTerm(object? value, ScheduleRequest? request)
    {
        var term = value is Dictionary<string, object?> dictionary
            ? new Dictionary<string, object?>(dictionary, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!term.ContainsKey("label"))
        {
            term["label"] = request?.TermLabel?.Trim() ?? string.Empty;
        }

        if (!term.ContainsKey("academic_term"))
        {
            term["academic_term"] = request?.AcademicTerm?.Trim() ?? string.Empty;
        }

        return term;
    }

    public static int? ParseWeekNumber(string? weekLabel)
    {
        var match = Regex.Match(weekLabel ?? string.Empty, @"\d+");
        return match.Success ? int.Parse(match.Value) : null;
    }

    private static string NormalizeSourceType(string sourceType, string sourceKey)
    {
        var cleaned = (sourceType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            return cleaned;
        }

        return sourceKey == "jw_schedule" ? "jw" : "mobile";
    }

    private static string ResolveSourceLabel(string sourceKey)
    {
        return sourceKey == "jw_schedule" ? "教务系统课表" : "移动教务课表";
    }

    private static string NormalizeTimestamp(object? value)
    {
        var raw = value?.ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(raw) ? DateTimeOffset.Now.ToString("O") : raw.Trim();
    }

    private sealed record ScheduleContext(
        string SessionId,
        StoredSessionDocument Session,
        StoredCredentialDocument Credentials,
        XkeStudentStore StudentStore,
        XkeScheduleStore ScheduleStore,
        StudentProfileDocument Profile);
}
