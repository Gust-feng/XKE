using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Network;
using Xke.Core.Notice;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapNetworkRoutes(WebApplication app)
    {
        var network = app.MapGroup("/api/network");

        network.MapGet("/config", async (NetworkService networkService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = networkService.GetConfig(sessionId);
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
            catch (NetworkException ex)
            {
                return NetworkError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        network.MapPost("/config", async (NetworkConfigRequest request, NetworkService networkService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await networkService.SaveConfigAsync(sessionId, request, context.RequestAborted);
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
            catch (NetworkException ex)
            {
                return NetworkError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        network.MapGet("/status", async (NetworkService networkService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await networkService.GetStatusAsync(sessionId, context.RequestAborted);
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
            catch (NetworkException ex)
            {
                return NetworkError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        network.MapPost("/maintain", async (NetworkActionRequest request, NetworkService networkService, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await networkService.MaintainAsync(sessionId, request, context.RequestAborted);
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
            catch (NetworkException ex)
            {
                return NetworkError(ex);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
