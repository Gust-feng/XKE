using Xke.Contracts.Requests;
using Xke.Core.Desktop;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapDesktopRoutes(WebApplication app)
    {
        var desktop = app.MapGroup("/api/desktop");

        desktop.MapGet("/settings", (DesktopSettingsService desktopSettingsService) =>
        {
            try
            {
                var result = desktopSettingsService.GetSettings();
                return Ok(result.Message, result.Payload);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        desktop.MapPost("/settings", (DesktopSettingsRequest request, DesktopSettingsService desktopSettingsService) =>
        {
            try
            {
                var result = desktopSettingsService.UpdateSettings(request);
                return Ok(result.Message, result.Payload);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        desktop.MapPost("/activate", (IDesktopWindowController desktopWindowController) =>
        {
            try
            {
                var payload = desktopWindowController.Activate();
                return Ok(payload.Activated ? "桌面窗口已激活" : "桌面窗口不可用", payload);
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    ok = false,
                    msg = ex.Message,
                    detail = ex.Message,
                }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
