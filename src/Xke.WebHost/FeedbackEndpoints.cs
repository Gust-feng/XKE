using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Feedback;
using Xke.Core.Notice;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapFeedbackRoutes(WebApplication app)
    {
        var feedback = app.MapGroup("/api/feedback");

        feedback.MapPost("/report", async (FeedbackRequest request, FeedbackService feedbackService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await feedbackService.ReportAsync(sessionId, request, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (FeedbackException ex)
            {
                return FeedbackError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
