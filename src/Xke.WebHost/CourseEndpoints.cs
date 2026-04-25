using Xke.Contracts.Api;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Course;
using Xke.Core.Notice;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapCourseRoutes(WebApplication app)
    {
        var course = app.MapGroup("/api/course");

        course.MapGet("/status", async (CourseService courseService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = courseService.GetStatus(sessionId);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (CourseException ex)
            {
                return CourseError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        course.MapPost("/connect", async (CourseService courseService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await courseService.ConnectAsync(sessionId, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (CourseException ex)
            {
                return CourseError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        course.MapGet("/rounds", async (CourseService courseService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                await EnsureCourseFeatureAllowedAsync(noticeService, authService.ResolveSessionStudentId(sessionId), "public_course_preview", "选课预览", context.RequestAborted);
                var refresh = string.Equals(ResolveQueryValue(context, "refresh"), "true", StringComparison.OrdinalIgnoreCase);
                var result = await courseService.GetRoundsAsync(sessionId, refresh, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (CourseException ex)
            {
                return CourseError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        course.MapPost("/preview", async (CoursePreviewRequest request, CourseService courseService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                await EnsureCourseFeatureAllowedAsync(noticeService, authService.ResolveSessionStudentId(sessionId), "public_course_preview", "选课预览", context.RequestAborted);
                var result = await courseService.GetPreviewAsync(sessionId, request.RoundId, request.Refresh, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (CourseException ex)
            {
                return CourseError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        course.MapPost("/grab/start", async (CourseGrabStartRequest request, CourseService courseService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                await EnsureCourseFeatureAllowedAsync(noticeService, authService.ResolveSessionStudentId(sessionId), "course_grab", "抢课", context.RequestAborted);
                var result = courseService.GetDeferredGrabStart();
                return Results.Json(new ApiEnvelope(Msg: result.Message, Data: result.Payload), statusCode: StatusCodes.Status501NotImplemented);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (CourseException ex)
            {
                return CourseError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        course.MapPost("/grab/stop", async (CourseService courseService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                await EnsureCourseFeatureAllowedAsync(noticeService, authService.ResolveSessionStudentId(sessionId), "course_grab", "抢课", context.RequestAborted);
                var result = courseService.GetDeferredGrabStop();
                return Results.Json(new ApiEnvelope(Msg: result.Message, Data: result.Payload), statusCode: StatusCodes.Status501NotImplemented);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (AuthException ex)
            {
                return AuthError(ex);
            }
            catch (CourseException ex)
            {
                return CourseError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
