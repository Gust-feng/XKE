using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Schedule;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapAuthRoutes(WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/captcha", async (JwCaptchaSubmitRequest request, AuthService authService, HttpContext context) =>
        {
            var result = await authService.SubmitProtocolCaptchaAsync(request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/login", async (LoginRequest request, AuthService authService, XkeWebHostOptions options, HttpContext context) =>
        {
            if (options.UseLegacyMobileAuthentication)
            {
                var legacyResult = await authService.LoginAsync(request, context.RequestAborted);
                return Ok(legacyResult.Message, legacyResult.Payload);
            }

            var result = await authService.BeginProtocolLoginAsync(request, cancellationToken: context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/reinitialize", async (LoginRequest request, AuthService authService, XkeWebHostOptions options, HttpContext context) =>
        {
            if (!options.UseLegacyMobileAuthentication)
            {
                var protocolResult = await authService.BeginProtocolLoginAsync(request, forceRemote: true, cancellationToken: context.RequestAborted);
                return Ok(protocolResult.Message, protocolResult.Payload);
            }

            var result = await authService.ReinitializeAsync(request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/trusted-login", (TrustedLoginRequest request, AuthService authService, HttpContext context) =>
        {
            var result = authService.TrustedLogin(request);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/connect", async (ConnectRequest request, AuthService authService, XkeWebHostOptions options, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            if (!options.UseLegacyMobileAuthentication)
            {
                var protocol = await authService.ConnectProtocolAsync(sessionId, context.RequestAborted);
                return Ok(protocol.Message, protocol.Payload);
            }
            var result = await authService.ConnectAsync(sessionId, request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        auth.MapGet("/session", (AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = authService.GetSession(sessionId);
            return Ok(result.Message, result.Payload);
        });

        auth.MapGet("/preferences", (AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = authService.GetPreferences(sessionId);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/preferences", (PreferencesRequest request, AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = authService.UpdatePreferences(sessionId, request);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/profile", (ProfileRequest request, AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = authService.UpdateProfile(sessionId, request);
            return Ok(result.Message, result.Payload);
        });

        auth.MapGet("/avatar", (AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var avatar = authService.GetAvatar(sessionId);
            return Results.File(avatar.Content, GetAvatarContentType(avatar.FileName));
        });

        auth.MapPost("/avatar", async (AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
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
        });

        auth.MapPost("/jw-captcha/submit", async (JwCaptchaSubmitRequest request, ScheduleService scheduleService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var result = await scheduleService.SubmitJwCaptchaAsync(sessionId, request, context.RequestAborted);
            return Ok(result.Message, result.Payload);
        });

        auth.MapPost("/logout", (AuthService authService, HttpContext context) =>
        {
            var sessionId = ResolveSessionId(context);
            var message = authService.Logout(sessionId);
            return Ok(message);
        });
    }
}
