using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Notice;
using Xke.Storage;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapUpdatesRoutes(WebApplication app)
    {
        var updates = app.MapGroup("/api/updates");

        updates.MapGet("/feed", async (NoticeService noticeService, XkeDataRoot dataRoot, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                var requestedStudentId = ResolveQueryValue(context, "student_id");
                var studentId = requestedStudentId;
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    var session = dataRoot.Sessions().Load(sessionId)
                        ?? throw new AuthException(StatusCodes.Status401Unauthorized, "会话不存在或已过期，请重新登录");
                    studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
                }

                var channel = ResolveQueryValue(context, "channel", "beta");
                var version = ResolveQueryValue(context, "version");
                if (string.IsNullOrWhiteSpace(version))
                {
                    version = ResolveQueryValue(context, "build");
                }

                var trigger = ResolveQueryValue(context, "trigger", "view");
                var feed = await noticeService.LoadFeedAsync(channel, version, trigger, studentId, context.RequestAborted);
                var sanitizedFeed = SanitizeFrontendRemotePayload(feed) as IReadOnlyDictionary<string, object?>
                                    ?? new Dictionary<string, object?>();
                var payload = new UpdatesFeedPayload(
                    Feed: sanitizedFeed,
                    StudentId: studentId);
                return Ok("更新与公告已加载", payload);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
