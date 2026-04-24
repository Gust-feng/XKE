using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Course;
using Xke.Core.Notice;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapAuthRoutes(WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/login", async (LoginRequest request, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                await EnsureStudentAllowedAsync(noticeService, request.User, "login", context.RequestAborted);
                var result = await authService.LoginAsync(request, context.RequestAborted);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapPost("/reinitialize", async (LoginRequest request, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                await EnsureStudentAllowedAsync(noticeService, request.User, "login", context.RequestAborted);
                var result = await authService.ReinitializeAsync(request, context.RequestAborted);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapPost("/trusted-login", async (TrustedLoginRequest request, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                await EnsureStudentAllowedAsync(noticeService, request.User, "login", context.RequestAborted);
                var result = authService.TrustedLogin(request);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapPost("/connect", async (ConnectRequest request, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = await authService.ConnectAsync(sessionId, request, context.RequestAborted);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapGet("/session", async (AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = authService.GetSession(sessionId);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapGet("/preferences", async (AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = authService.GetPreferences(sessionId);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapPost("/preferences", async (PreferencesRequest request, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = authService.UpdatePreferences(sessionId, request);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapPost("/profile", async (ProfileRequest request, AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var result = authService.UpdateProfile(sessionId, request);
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapGet("/avatar", async (AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                var path = authService.GetAvatarPath(sessionId);
                return Results.File(path, GetAvatarContentType(path));
            }
            catch (BlockedAccessException ex)
            {
                return BlockedError(ex);
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

        auth.MapPost("/avatar", async (AuthService authService, NoticeService noticeService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                await EnsureSessionAllowedAsync(authService, noticeService, sessionId, context.RequestAborted);
                if (!context.Request.HasFormContentType)
                {
                    throw new AuthException(StatusCodes.Status400BadRequest, "头像上传必须使用 multipart/form-data");
                }

                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var file = form.Files.FirstOrDefault()
                    ?? throw new AuthException(StatusCodes.Status400BadRequest, "缺少头像文件");
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, context.RequestAborted);
                var result = authService.SaveAvatar(sessionId, file.FileName, buffer.ToArray());
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
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        auth.MapPost("/logout", (AuthService authService, CourseService courseService, HttpContext context) =>
        {
            try
            {
                var sessionId = ResolveSessionId(context);
                var message = authService.Logout(sessionId);
                courseService.ClearSession(sessionId);
                return Ok(message);
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
