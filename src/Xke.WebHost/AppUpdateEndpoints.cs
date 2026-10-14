using Xke.Core.Releases;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapAppUpdateRoutes(WebApplication app)
    {
        var updates = app.MapGroup("/api/app/updates");
        updates.MapGet("", (AppUpdateService service) => Ok("版本信息", service.State));
        updates.MapPost("/check", async (AppUpdateService service, HttpContext context)
            => Ok("更新检查完成", await service.CheckAsync(context.RequestAborted)));
        updates.MapPost("/download", (AppUpdateService service)
            => Ok("更新下载状态", service.RequestDownload()));
        updates.MapPost("/apply", (AppUpdateService service) => Ok("更新安装状态", service.RequestApply()));
    }
}
