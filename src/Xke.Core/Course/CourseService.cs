using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Course;

public sealed class CourseService
{
    private const string ConnectionRequiredReason = "course_connection_required";
    private const string GrabDeferredReason = CourseDeferredPolicy.GrabReason;
    private const string DeferredGrabMessage = CourseDeferredPolicy.GrabMessage;
    private readonly XkeDataRoot _dataRoot;
    private readonly object _connectedSessionsGate = new();
    private readonly HashSet<string> _connectedSessions = new(StringComparer.Ordinal);
    private readonly ICourseClient _courseClient;

    public CourseService(XkeDataRoot dataRoot, ICourseClient courseClient)
    {
        _dataRoot = dataRoot;
        _courseClient = courseClient;
    }

    public (string Message, CourseStatusPayload Payload) GetStatus(string sessionId)
    {
        var context = LoadContext(sessionId);
        return (
            "选课中心状态已加载",
            new CourseStatusPayload(
                StudentId: context.StudentId,
                StorageDir: context.CourseStore.RelativeDirectory(),
                CourseConnection: BuildConnectionState(sessionId),
                Features: BuildFeatureState(),
                GrabState: BuildDeferredGrabState()));
    }

    public async Task<(string Message, CourseConnectPayload Payload)> ConnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        await _courseClient.ConnectAsync(sessionId, context.StudentId, cancellationToken).ConfigureAwait(false);
        MarkSessionConnected(sessionId);
        return (
            "选课中心连接成功",
            new CourseConnectPayload(
                StudentId: context.StudentId,
                StorageDir: context.CourseStore.RelativeDirectory(),
                CourseConnection: BuildConnectionState(sessionId)));
    }

    public async Task<(string Message, CourseRoundsPayload Payload)> GetRoundsAsync(string sessionId, bool refresh, CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        EnsureConnected(sessionId);

        if (!refresh)
        {
            var cachedRounds = context.CourseStore.LoadRounds();
            if (cachedRounds is not null)
            {
                return ("选课轮次已从本地缓存加载", CloneRoundsPayload(cachedRounds, cached: true));
            }
        }

        CourseRoundsFetchResult result;
        try
        {
            result = await _courseClient.LoadRoundsAsync(sessionId, context.StudentId, refresh, cancellationToken).ConfigureAwait(false);
        }
        catch (CourseException ex) when (IsConnectionRequired(ex))
        {
            ClearSession(sessionId);
            throw;
        }

        var payload = new CourseRoundsPayload(
            Rounds: result.Rounds,
            Count: result.Rounds.Count,
            Cached: false,
            UpdatedAt: DateTimeOffset.Now.ToString("O"),
            Warnings: result.Warnings);
        context.CourseStore.SaveRounds(payload, result.RawPayload);
        return ("选课轮次已刷新", payload);
    }

    public async Task<(string Message, CoursePreviewPayload Payload)> GetPreviewAsync(string sessionId, string roundId, bool refresh, CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        EnsureConnected(sessionId);
        var resolvedRoundId = NormalizeRequired(roundId, "选课轮次不能为空");

        if (!refresh)
        {
            var cachedPreview = context.CourseStore.LoadPreview(resolvedRoundId);
            if (cachedPreview is not null)
            {
                return ("课程预览已从本地缓存加载", ClonePreviewPayload(cachedPreview, cached: true));
            }
        }

        CoursePreviewFetchResult result;
        try
        {
            result = await _courseClient.LoadPreviewAsync(sessionId, context.StudentId, resolvedRoundId, refresh, cancellationToken).ConfigureAwait(false);
        }
        catch (CourseException ex) when (IsConnectionRequired(ex))
        {
            ClearSession(sessionId);
            throw;
        }

        var payload = new CoursePreviewPayload(
            RoundId: resolvedRoundId,
            Courses: result.Courses,
            Count: result.Courses.Count,
            Cached: false,
            UpdatedAt: DateTimeOffset.Now.ToString("O"),
            Warnings: result.Warnings);
        context.CourseStore.SavePreview(payload, result.RawPayload);
        return ("课程预览已刷新", payload);
    }

    public (string Message, CourseDeferredPayload Payload) GetDeferredGrabStart()
    {
        // course grab intentionally deferred; do not add real polling/JW mutation in this slice
        return (
            DeferredGrabMessage,
            new CourseDeferredPayload(
                Reason: GrabDeferredReason,
                GrabState: BuildDeferredGrabState()));
    }

    public (string Message, CourseDeferredPayload Payload) GetDeferredGrabStop()
    {
        // course grab intentionally deferred; do not add real polling/JW mutation in this slice
        return (
            DeferredGrabMessage,
            new CourseDeferredPayload(
                Reason: GrabDeferredReason,
                GrabState: BuildDeferredGrabState()));
    }

    public void ClearSession(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        lock (_connectedSessionsGate)
        {
            _connectedSessions.Remove(cleaned);
        }
    }

    private CourseContext LoadContext(string sessionId)
    {
        var cleanedSessionId = NormalizeRequired(sessionId, "会话不存在或已过期，请重新登录");
        var session = _dataRoot.Sessions().Load(cleanedSessionId)
            ?? throw new CourseException(401, "会话不存在或已过期，请重新登录");
        var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
        var studentStore = _dataRoot.ForStudent(studentId);
        return new CourseContext(
            Session: session,
            StudentId: studentId,
            CourseStore: studentStore.Course());
    }

    private void EnsureConnected(string sessionId)
    {
        if (IsSessionConnected(sessionId))
        {
            return;
        }

        throw new CourseException(409, BuildConnectionRequiredMessage(), ConnectionRequiredReason);
    }

    private bool IsSessionConnected(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        lock (_connectedSessionsGate)
        {
            return _connectedSessions.Contains(cleaned);
        }
    }

    private void MarkSessionConnected(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        lock (_connectedSessionsGate)
        {
            _connectedSessions.Add(cleaned);
        }
    }

    private CourseConnectionState BuildConnectionState(string sessionId)
    {
        var connected = IsSessionConnected(sessionId);
        if (connected)
        {
            return new CourseConnectionState(
                Connected: true,
                Status: "connected",
                Reason: string.Empty,
                Message: "课程预览已连接，可加载轮次和课程列表",
                UpdatedAt: DateTimeOffset.Now.ToString("O"));
        }

        var status = _courseClient.IsAvailable ? "disconnected" : "deferred";
        var reason = _courseClient.IsAvailable ? ConnectionRequiredReason : _courseClient.UnavailableReason;
        var message = _courseClient.IsAvailable ? BuildConnectionRequiredMessage() : _courseClient.UnavailableMessage;
        return new CourseConnectionState(
            Connected: false,
            Status: status,
            Reason: reason,
            Message: message,
            UpdatedAt: DateTimeOffset.Now.ToString("O"));
    }

    private static CourseFeatureState BuildFeatureState()
    {
        return new CourseFeatureState(
            Rounds: true,
            Preview: true,
            Grab: false,
            GrabReason: GrabDeferredReason);
    }

    private static CourseGrabDeferredState BuildDeferredGrabState()
    {
        return new CourseGrabDeferredState(
            Status: "deferred",
            Running: false,
            Deferred: true,
            Reason: GrabDeferredReason,
            Message: DeferredGrabMessage);
    }

    private static CourseRoundsPayload CloneRoundsPayload(CourseRoundsPayload payload, bool cached)
    {
        return new CourseRoundsPayload(
            Rounds: payload.Rounds,
            Count: payload.Count,
            Cached: cached,
            UpdatedAt: payload.UpdatedAt,
            Warnings: payload.Warnings);
    }

    private static CoursePreviewPayload ClonePreviewPayload(CoursePreviewPayload payload, bool cached)
    {
        return new CoursePreviewPayload(
            RoundId: payload.RoundId,
            Courses: payload.Courses,
            Count: payload.Count,
            Cached: cached,
            UpdatedAt: payload.UpdatedAt,
            Warnings: payload.Warnings);
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new CourseException(400, errorMessage);
        }

        return cleaned;
    }

    private static string BuildConnectionRequiredMessage()
    {
        return "请先连接教务系统，再加载选课轮次和课程预览";
    }

    private static bool IsConnectionRequired(CourseException exception)
    {
        return string.Equals(exception.Reason, ConnectionRequiredReason, StringComparison.Ordinal);
    }

    private sealed record CourseContext(
        StoredSessionDocument Session,
        string StudentId,
        XkeCourseStore CourseStore
    );
}
