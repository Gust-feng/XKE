using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Notice;
using Xke.Core.Telemetry;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapTelemetryRoutes(WebApplication app)
    {
        var telemetry = app.MapGroup("/api/telemetry");

        telemetry.MapPost("/report", async (TelemetryReportRequest request, TelemetryService telemetryService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await telemetryService.ReportAsync(sessionId, request, context.RequestAborted);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (TelemetryException ex)
            {
                return TelemetryError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        telemetry.MapGet("/overview", async (TelemetryService telemetryService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = telemetryService.GetOverview(sessionId);
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (TelemetryException ex)
            {
                return TelemetryError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        telemetry.MapGet("/profile", async (TelemetryService telemetryService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = telemetryService.GetProfile(sessionId, ResolveQueryValue(context, "student_id"));
                return Ok(result.Message, result.Payload);
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
            }
            catch (TelemetryException ex)
            {
                return TelemetryError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
