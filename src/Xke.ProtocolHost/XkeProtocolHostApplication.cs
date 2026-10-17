using System.Security.Cryptography;
using System.Text;
using Xke.Contracts.Api;
using Xke.Contracts.Requests;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;

namespace Xke.ProtocolHost;

public static class XkeProtocolHostApplication
{
    public const string ApiKeyHeader = "X-XKE-Api-Key";

    public static WebApplication Build(ProtocolHostOptions options, Action<IServiceCollection>? configureServices = null)
    {
        options.Validate();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls(options.Urls.ToArray());
        builder.Services.Configure<RouteHandlerOptions>(route => route.ThrowOnBadRequest = true);
        builder.Services.AddSingleton(new XkeDataRoot(options.DataRootPath));
        builder.Services.AddHttpClient("captcha", client => client.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddSingleton<ICaptchaRecognizer>(services => new DdddOcrCaptchaRecognizer(
            services.GetRequiredService<IHttpClientFactory>().CreateClient("captcha"), options.DdddOcrEndpoint));
        builder.Services.AddSingleton<Func<string, string, DirectJwLoginProtocol>>(_ => (user, password) => new(user, password));
        builder.Services.AddSingleton<ProtocolRuntime>();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseStatusCodePages(async status =>
            await ErrorAsync(status.HttpContext, status.HttpContext.Response.StatusCode, "请求格式无效或接口不存在"));
        var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.ApiKey));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path != "/health")
            {
                var supplied = context.Request.Headers[ApiKeyHeader];
                if (supplied.Count != 1 || !CryptographicOperations.FixedTimeEquals(keyHash,
                        SHA256.HashData(Encoding.UTF8.GetBytes(supplied[0] ?? ""))))
                {
                    await ErrorAsync(context, 401, "API key 无效或缺失");
                    return;
                }
            }
            try { await next(context); }
            catch (BadHttpRequestException ex) { await ErrorAsync(context, ex.StatusCode, "请求格式无效"); }
            catch (JwScheduleException ex) { await ErrorAsync(context, ex.StatusCode == 500 ? 502 : ex.StatusCode, ex.Message); }
            catch (HttpRequestException) { await ErrorAsync(context, 502, "无法连接教务服务"); }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            {
                await ErrorAsync(context, 504, "协议请求超时");
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (IOException) { await ErrorAsync(context, 500, "业务数据文件读写失败"); }
            catch (UnauthorizedAccessException) { await ErrorAsync(context, 500, "协议服务数据目录不可写"); }
        });

        app.MapGet("/health", () => Results.Json(new { status = "ok", service = "xke-protocol" }));
        app.MapPost("/api/auth/login", async (LoginRequest request, ProtocolRuntime runtime, CancellationToken token)
            => Ok("教务登录成功", await runtime.LoginAsync(request, token)));
        app.MapGet("/api/auth/session", async (ProtocolRuntime runtime, CancellationToken token)
            => Ok("内存会话", await runtime.GetConnectionAsync(token)));
        app.MapPost("/api/auth/logout", async (ProtocolRuntime runtime, CancellationToken token) =>
        {
            await runtime.LogoutAsync(token);
            return Ok("已断开教务", new { connected = false });
        });
        app.MapGet("/api/auth/profile", async (ProtocolRuntime runtime, CancellationToken token)
            => Ok("学生资料获取成功", await runtime.FetchProfileAsync(token)));
        app.MapGet("/api/schedule/week", async (int? week, string? academic_term, ProtocolRuntime runtime, CancellationToken token)
            => Ok("周课表获取成功", await runtime.FetchWeekAsync(week, academic_term?.Trim() ?? "", token)));
        app.MapGet("/api/schedule/semester", async (string? academic_term, ProtocolRuntime runtime, CancellationToken token)
            => Ok("学期课表获取成功", await runtime.FetchSemesterAsync(academic_term?.Trim() ?? "", token)));
        return app;
    }

    private static IResult Ok(string message, object data) => Results.Json(new ApiEnvelope(Msg: message, Data: data));
    private static Task ErrorAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(message, message), context.RequestAborted);
    }
}
