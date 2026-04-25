using Xke.Contracts.Responses;
using Xke.Core.Schedule;

namespace Xke.Core.Course;

public sealed class TraditionalJwCourseClient : ICourseClient
{
    private const string ConnectionRequiredReason = "course_connection_required";
    private const string PreviewFailedReason = "course_preview_failed";
    private readonly TraditionalJwSessionRuntimeStore _sessions;

    public TraditionalJwCourseClient(TraditionalJwSessionRuntimeStore sessions)
    {
        _sessions = sessions;
    }

    public bool IsAvailable => true;
    public string UnavailableReason => string.Empty;
    public string UnavailableMessage => string.Empty;

    public Task ConnectAsync(string sessionId, string studentId, CancellationToken cancellationToken = default)
    {
        _ = studentId;
        _ = cancellationToken;
        _ = ResolveClient(sessionId);
        return Task.CompletedTask;
    }

    public async Task<CourseRoundsFetchResult> LoadRoundsAsync(
        string sessionId,
        string studentId,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        _ = refresh;
        var client = ResolveClient(sessionId);
        try
        {
            var rounds = await client.FetchCourseRoundsAsync(cancellationToken).ConfigureAwait(false);
            var warnings = rounds.Count == 0 ? new[] { "当前没有可预览的选课轮次" } : Array.Empty<string>();
            return new CourseRoundsFetchResult(
                Rounds: rounds,
                Warnings: warnings,
                RawPayload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = "traditional_jw",
                    ["mode"] = "read_only_preview",
                    ["student_id"] = studentId,
                    ["round_count"] = rounds.Count,
                });
        }
        catch (TraditionalJwSessionExpiredException ex)
        {
            _sessions.Remove(sessionId);
            throw new CourseException(409, $"传统教务会话已失效，请重新连接教务系统。{ex.Message}", ConnectionRequiredReason);
        }
        catch (JwScheduleException ex)
        {
            throw new CourseException(ex.StatusCode, ex.Message, PreviewFailedReason);
        }
    }

    public async Task<CoursePreviewFetchResult> LoadPreviewAsync(
        string sessionId,
        string studentId,
        string roundId,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        _ = refresh;
        var client = ResolveClient(sessionId);
        try
        {
            var courses = await client.FetchCoursePreviewAsync(roundId, cancellationToken).ConfigureAwait(false);
            var warnings = courses.Count == 0 ? new[] { "当前轮次没有可预览课程" } : Array.Empty<string>();
            return new CoursePreviewFetchResult(
                RoundId: roundId,
                Courses: courses,
                Warnings: warnings,
                RawPayload: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = "traditional_jw",
                    ["mode"] = "read_only_preview",
                    ["student_id"] = studentId,
                    ["round_id"] = roundId,
                    ["course_count"] = courses.Count,
                });
        }
        catch (TraditionalJwSessionExpiredException ex)
        {
            _sessions.Remove(sessionId);
            throw new CourseException(409, $"传统教务会话已失效，请重新连接教务系统。{ex.Message}", ConnectionRequiredReason);
        }
        catch (JwScheduleException ex)
        {
            throw new CourseException(ex.StatusCode, ex.Message, PreviewFailedReason);
        }
    }

    private TraditionalJwClient ResolveClient(string sessionId)
    {
        if (!_sessions.TryGet(sessionId, out var client) || !client.HasBootstrappedJwSession)
        {
            throw new CourseException(409, "请先连接教务系统，再加载选课轮次和课程预览", ConnectionRequiredReason);
        }

        return client;
    }
}
