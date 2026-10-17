using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Schedule;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapScheduleRoutes(WebApplication app)
    {
        var schedule = app.MapGroup("/api/schedule");
        schedule.MapGet("/refresh-status", (AuthService authService, ScheduleRefreshScheduler scheduler, HttpContext context) =>
        {
            authService.ResolveSessionStudentId(ResolveSessionId(context));
            return Ok("后台课表刷新状态", scheduler.State);
        });

        schedule.MapGet("/semester", async (ScheduleService service, HttpContext context) =>
        {
            var result = await service.FetchSemesterAsync(ResolveSessionId(context), ResolveQueryValue(context, "academic_term"), context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });
        schedule.MapGet("/semester/local", (ScheduleService service, HttpContext context) =>
        {
            var result = service.LoadLocalSemester(ResolveSessionId(context), ResolveQueryValue(context, "academic_term"));
            return Ok(result.Message, result.Payload);
        });
        schedule.MapGet("/local", (ScheduleService scheduleService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var weekLabel = ResolveQueryValue(context, "week_label");
            var source = ResolveQueryValue(context, "source", "preferred");
            var result = scheduleService.LoadLocal(sessionId, weekLabel, source, ResolveQueryValue(context, "academic_term"));
            return Ok(result.Message, result.Payload);
        });

        schedule.MapPost("/preferred", async (ScheduleRequest request, ScheduleService scheduleService, AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = await scheduleService.FetchPreferredAsync(sessionId, request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        schedule.MapPost("/cookie", async (ScheduleRequest request, ScheduleService scheduleService, AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = await scheduleService.FetchCookieAsync(sessionId, request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });
    }
}
