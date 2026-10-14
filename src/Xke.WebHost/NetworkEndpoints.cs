using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Network;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapNetworkRoutes(WebApplication app)
    {
        var network = app.MapGroup("/api/network");
        network.MapGet("/summary", async (CampusSelfServiceSummaryService summaries, HttpContext context) =>
        {
            var summary = await summaries.GetSummaryAsync(ResolveSessionId(context), context.RequestAborted);
            return Ok(summary.Message, summary);
        });
        network.MapGet("/maintenance", (NetworkService networkService, NetworkMaintenanceScheduler scheduler, HttpContext context) =>
        {
            networkService.GetConfig(ResolveSessionId(context));
            return Ok("后台保活状态", scheduler.State);
        });

        network.MapGet("/config", (NetworkService networkService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = networkService.GetConfig(sessionId);
            return Ok(result.Message, result.Payload);
        });

        network.MapPost("/config", (NetworkConfigRequest request, NetworkService networkService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = networkService.SaveConfig(sessionId, request);
            return Ok(result.Message, result.Payload);
        });

        network.MapGet("/status", async (NetworkService networkService, AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = await networkService.GetStatusAsync(sessionId, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        network.MapPost("/maintain", async (NetworkActionRequest request, NetworkService networkService, AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = await networkService.MaintainAsync(sessionId, request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });
    }
}
