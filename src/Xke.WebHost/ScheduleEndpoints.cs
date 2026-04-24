using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Notice;
using Xke.Core.Schedule;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapScheduleRoutes(WebApplication app)
    {
        var schedule = app.MapGroup("/api/schedule");

        schedule.MapGet("/local", async (ScheduleService scheduleService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var weekLabel = ResolveQueryValue(context, "week_label");
                var source = ResolveQueryValue(context, "source", "preferred");
                var result = scheduleService.LoadLocal(sessionId, weekLabel, source);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (ScheduleException ex)
            {
                return ScheduleError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        schedule.MapPost("/preferred", async (ScheduleRequest request, ScheduleService scheduleService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await scheduleService.FetchPreferredAsync(sessionId, request, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (ScheduleException ex)
            {
                return ScheduleError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        schedule.MapPost("/cookie", async (ScheduleRequest request, ScheduleService scheduleService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await scheduleService.FetchCookieAsync(sessionId, request, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (ScheduleException ex)
            {
                return ScheduleError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
