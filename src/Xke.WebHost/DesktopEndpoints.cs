using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Desktop;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapDesktopRoutes(WebApplication app)
    {
        var desktop = app.MapGroup("/api/desktop");

        desktop.MapGet("/settings", (DesktopSettingsService desktopSettingsService) =>
        {
            var result = desktopSettingsService.GetSettings();
            return Ok(result.Message, result.Payload);
        });

        desktop.MapPost("/settings", (DesktopSettingsRequest request, DesktopSettingsService desktopSettingsService, Xke.Core.Releases.AppUpdateService updates) =>
        {
            var result = desktopSettingsService.UpdateSettings(request);
            updates.SetAutomaticEnabled(result.Payload.Settings.AutoUpdateEnabled);
            return Ok(result.Message, result.Payload);
        });

        desktop.MapPost("/activate", (IDesktopWindowController desktopWindowController) =>
        {
            var payload = desktopWindowController.Activate();
            return Ok(payload.Activated ? "桌面窗口已激活" : "桌面窗口不可用", payload);
        });

        desktop.MapPost("/auth-bootstrap", (AuthService authService, XkeWebHostOptions options) =>
        {
            if (!IsDesktopWebViewHostMode(options.HostMode))
            {
                return Error(StatusCodes.Status404NotFound, "当前运行模式不支持桌面可信恢复");
            }

            var result = authService.DesktopBootstrap();
            return Ok(result.Message, new DesktopAuthBootstrapPayload(result.User, result.Payload));
        });
    }

    private static bool IsDesktopWebViewHostMode(string? hostMode)
    {
        return string.Equals(hostMode?.Trim(), "desktop-webview", StringComparison.OrdinalIgnoreCase);
    }
}
