using Xke.Core.Update;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    private static void MapUpdateRoutes(WebApplication app)
    {
        var updates = app.MapGroup("/api/update");

        updates.MapGet("/status", async (UpdateService updateService, XkeWebHostOptions options, HttpContext context) =>
        {
            try
            {
                var status = await updateService.GetStatusAsync(
                    IsDesktopInstallSupported(options),
                    ResolveQueryValue(context, "trigger", "view"),
                    context.RequestAborted).ConfigureAwait(false);
                return Ok("更新状态已加载", status);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        updates.MapPost("/download", async (UpdateService updateService, HttpContext context) =>
        {
            try
            {
                var job = await updateService.StartDownloadAsync(context.RequestAborted).ConfigureAwait(false);
                return Ok("更新包下载任务已创建", job);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        updates.MapGet("/jobs/{jobId}", (string jobId, UpdateService updateService) =>
        {
            try
            {
                return Ok("更新任务已加载", updateService.GetJob(jobId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        updates.MapPost("/prepare-install", async (UpdateService updateService, XkeWebHostOptions options, HttpContext context) =>
        {
            try
            {
                var payload = await updateService.PrepareInstallAsync(
                    IsDesktopInstallSupported(options),
                    Environment.ProcessPath ?? AppContext.BaseDirectory,
                    context.RequestAborted).ConfigureAwait(false);
                return Ok(payload.Prepared ? "安装计划已准备" : payload.Message, payload);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }

    private static bool IsDesktopInstallSupported(XkeWebHostOptions options)
    {
        return string.Equals(options.HostMode, "desktop-webview", StringComparison.OrdinalIgnoreCase)
               || options.HostMode.Contains("desktop", StringComparison.OrdinalIgnoreCase);
    }
}
