using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xke.Contracts.Api;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Core.Course;
using Xke.Core.Desktop;
using Xke.Core.Notice;
using Xke.Core.Runtime;
using Xke.Core.Schedule;
using Xke.Core.Network;
using Xke.Core.Telemetry;
using Xke.Core.Feedback;
using Xke.Storage;

namespace Xke.WebHost;

public static partial class XkeWebHostApplication
{
    public static WebApplication Create(string[]? args = null, XkeWebHostOptions? options = null)
    {
        var hostOptions = options ?? new XkeWebHostOptions();
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        if (hostOptions.Urls.Count > 0)
        {
            builder.WebHost.UseUrls(hostOptions.Urls.ToArray());
        }

        var resolvedPaths = XkePathResolver.Resolve(hostOptions.WebRootPath);
        var dotNetRoot = ResolveDotNetWorkspaceRoot(resolvedPaths.AppBaseDirectory);
        builder.Services.AddSingleton(hostOptions);
        builder.Services.AddSingleton(resolvedPaths);
        builder.Services.AddSingleton(XkeDataRootFactory.CreateDefault(
            appRoot: dotNetRoot,
            workspaceRoot: dotNetRoot,
            explicitDataRoot: hostOptions.AuthDataRootPath));
        builder.Services.AddSingleton<IJwAuthClient>(_ =>
            hostOptions.JwAuthClient ?? new NativeJwAuthClient(new HttpClient()));
        builder.Services.AddSingleton<IJwScheduleClient>(_ =>
            hostOptions.JwScheduleClient ?? new NativeJwScheduleClient(new HttpClient()));
        builder.Services.AddSingleton<MobileSessionRuntimeStore>();
        builder.Services.AddSingleton<ICourseClient>(_ =>
            hostOptions.CourseClient ?? new UnavailableCourseClient());
        builder.Services.AddSingleton(_ =>
            hostOptions.NetworkHttpMessageHandler is null
                ? new HttpClient()
                : new HttpClient(hostOptions.NetworkHttpMessageHandler, disposeHandler: false));
        builder.Services.AddSingleton<INetworkCommandExecutor>(_ =>
            hostOptions.NetworkCommandExecutor ?? new ProcessNetworkCommandExecutor());
        builder.Services.AddSingleton(new NoticeServiceOptions
        {
            AppRoot = dotNetRoot,
            PublicKeyPem = hostOptions.NoticeSigningPublicKeyPem,
            DefaultChannel = "beta",
        });
        builder.Services.AddSingleton<INoticeRemoteSource>(_ =>
            hostOptions.NoticeRemoteSource ?? new HttpNoticeRemoteSource(new HttpClient()));
        builder.Services.AddSingleton<NoticeService>();
        builder.Services.AddSingleton<DesktopSettingsService>();
        builder.Services.AddSingleton<IDesktopWindowController>(_ =>
            hostOptions.DesktopWindowController ?? new NoopDesktopWindowController());
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<CourseService>();
        builder.Services.AddSingleton<ScheduleService>();
        builder.Services.AddSingleton<NetworkService>();
        builder.Services.AddSingleton<ITelemetryTransport, LocalOnlyTelemetryTransport>();
        builder.Services.AddSingleton<TelemetryService>();
        builder.Services.AddSingleton<IFeedbackTransport, LocalFeedbackOutboxTransport>();
        builder.Services.AddSingleton<FeedbackService>();

        builder.Services.AddCors(cors =>
        {
            cors.AddPolicy("LoopbackOnly", policy =>
            {
                policy.SetIsOriginAllowed(IsLoopbackOrigin);
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
            });
        });

        var app = builder.Build();

        app.UseCors("LoopbackOnly");
        app.Use(ApplySessionHeaderCompatibility);
        app.Use(ApplyBetaGuard);
        app.Use(ApplyNoCacheHeaders);

        ConfigureStaticFiles(app, resolvedPaths);
        MapShellRoutes(app);

        return app;
    }

    private static bool IsLoopbackOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback;
    }

    private static Task ApplySessionHeaderCompatibility(HttpContext context, Func<Task> next)
    {
        if (context.Request.Path.StartsWithSegments(SessionConstants.ApiBasePath)
            && !context.Request.Query.ContainsKey(SessionConstants.SessionQueryName)
            && context.Request.Headers.TryGetValue(SessionConstants.SessionHeaderName, out var sessionHeader)
            && !string.IsNullOrWhiteSpace(sessionHeader.ToString()))
        {
            var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in context.Request.Query)
            {
                query[pair.Key] = pair.Value.ToString();
            }

            query[SessionConstants.SessionQueryName] = sessionHeader.ToString().Trim();
            context.Request.QueryString = QueryString.Create(query);
        }

        return next();
    }

    private static async Task ApplyBetaGuard(HttpContext context, Func<Task> next)
    {
        if (context.Request.Path.StartsWithSegments(SessionConstants.ApiBasePath)
            && context.Request.Path != "/api/config"
            && context.Request.Path != "/api/health"
            && WebHostRuntime.IsBetaExpired())
        {
            ApplyNoCacheHeaders(context.Response);
            await Results.Json(new
            {
                ok = false,
                msg = WebHostRuntime.BetaMessage(),
                detail = WebHostRuntime.BetaMessage(),
                beta = new HostBetaResponse(WebHostRuntime.BetaEnabled(), WebHostRuntime.BetaMessage()),
            }, statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
            return;
        }

        await next();
    }

    private static async Task ApplyNoCacheHeaders(HttpContext context, Func<Task> next)
    {
        context.Response.OnStarting(() =>
        {
            ApplyNoCacheHeaders(context.Response);
            return Task.CompletedTask;
        });

        await next();
    }

    private static void ApplyNoCacheHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
    }

    private static void ConfigureStaticFiles(WebApplication app, XkeResolvedPaths resolvedPaths)
    {
        if (!resolvedPaths.HasWebRoot || resolvedPaths.WebRootPath is null)
        {
            return;
        }

        var assetsRoot = Path.Combine(resolvedPaths.WebRootPath, "assets");
        if (!Directory.Exists(assetsRoot))
        {
            return;
        }

        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".mjs"] = "text/javascript";

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/assets",
            FileProvider = new PhysicalFileProvider(assetsRoot),
            ContentTypeProvider = provider,
        });
    }

    private static void MapShellRoutes(WebApplication app)
    {
        app.MapGet("/api/health", (HttpContext context, XkeResolvedPaths paths, AuthService authService) =>
        {
            var frontendDiagnostics = BuildFrontendDiagnostics(paths);
            return Results.Json(new HostHealthResponse(
                Status: "ok",
                Sessions: authService.SessionCount,
                WebRootFound: paths.HasWebRoot,
                WebRoot: paths.WebRootPath ?? string.Empty,
                BaseUrl: BuildBaseUrl(context),
                FrontendBuild: frontendDiagnostics.FrontendBuild,
                StaticAssetHashes: frontendDiagnostics.StaticAssetHashes));
        });

        app.MapGet("/api/config", async (HttpContext context, XkeResolvedPaths paths, XkeWebHostOptions options, NoticeService noticeService) =>
        {
            var version = ResolveQueryValue(context, "version");
            var channel = ResolveQueryValue(context, "channel", "beta");
            var feed = await noticeService.LoadFeedAsync(channel, version, "startup", cancellationToken: context.RequestAborted);
            var warnings = ResolveWarnings(feed.GetValueOrDefault("warnings"));
            var config = feed.GetValueOrDefault("config") as Dictionary<string, object?> ?? new Dictionary<string, object?>();

            return Results.Json(new HostConfigResponse(
                User: string.Empty,
                Zbid: string.Empty,
                PreloginMode: "capture",
                Beta: new HostBetaResponse(WebHostRuntime.BetaEnabled(), WebHostRuntime.BetaMessage()),
                Development: new HostDevelopmentResponse(
                    Channel: options.RuntimeChannel,
                    HostMode: options.HostMode,
                    BaseUrl: BuildBaseUrl(context),
                    WebRoot: paths.WebRootPath ?? string.Empty,
                    WebRootFound: paths.HasWebRoot),
                Remote: new HostRemoteResponse(
                    Source: feed.GetValueOrDefault("source")?.ToString() ?? string.Empty,
                    Warnings: warnings,
                    Config: config)));
        });

        MapUpdatesRoutes(app);
        MapDesktopRoutes(app);
        MapAuthRoutes(app);
        MapCourseRoutes(app);
        MapScheduleRoutes(app);
        MapNetworkRoutes(app);
        MapTelemetryRoutes(app);
        MapFeedbackRoutes(app);

        app.MapFallback(async (HttpContext context, XkeResolvedPaths paths) =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                await Results.Json(new { detail = "Not found" }, statusCode: StatusCodes.Status404NotFound)
                    .ExecuteAsync(context);
                return;
            }

            if (!paths.HasWebRoot || paths.WebRootPath is null)
            {
                await Results.Json(new { detail = "web root not found" }, statusCode: StatusCodes.Status404NotFound)
                    .ExecuteAsync(context);
                return;
            }

            var webRoot = Path.GetFullPath(paths.WebRootPath);
            var relativePath = context.Request.Path.Value?.TrimStart('/') ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                var candidate = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (IsPathInsideRoot(webRoot, candidate) && File.Exists(candidate))
                {
                    await context.Response.SendFileAsync(candidate);
                    return;
                }
            }

            var indexPath = Path.Combine(paths.WebRootPath, "index.html");
            if (File.Exists(indexPath))
            {
                await context.Response.SendFileAsync(indexPath);
                return;
            }

            await Results.Json(new { detail = "index not found" }, statusCode: StatusCodes.Status404NotFound)
                .ExecuteAsync(context);
        });
    }

    internal static string ResolveBaseUrl(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        return addresses?.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            ?? app.Urls.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            ?? "http://127.0.0.1";
    }

    private static string BuildBaseUrl(HttpContext context)
    {
        return $"{context.Request.Scheme}://{context.Request.Host}";
    }

    private static bool IsPathInsideRoot(string rootPath, string candidatePath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(rootPath), Path.GetFullPath(candidatePath));
        return relative != "."
               && !Path.IsPathRooted(relative)
               && relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static FrontendDiagnostics BuildFrontendDiagnostics(XkeResolvedPaths paths)
    {
        if (!paths.HasWebRoot || paths.WebRootPath is null)
        {
            return new FrontendDiagnostics(string.Empty, new Dictionary<string, string>());
        }

        var assetHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in new[]
                 {
                     "index.html",
                     "assets/app.js",
                     "assets/modules/profile.js",
                     "assets/modules/http.js",
                 })
        {
            var absolutePath = Path.Combine(paths.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            try
            {
                var hash = SHA256.HashData(File.ReadAllBytes(absolutePath));
                assetHashes[relativePath] = Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch (IOException)
            {
                assetHashes[relativePath] = "unavailable";
            }
            catch (UnauthorizedAccessException)
            {
                assetHashes[relativePath] = "unavailable";
            }
        }

        return new FrontendDiagnostics(ResolveFrontendBuild(paths.WebRootPath), assetHashes);
    }

    private static string ResolveFrontendBuild(string webRootPath)
    {
        var appJsPath = Path.Combine(webRootPath, "assets", "app.js");
        if (!File.Exists(appJsPath))
        {
            return string.Empty;
        }

        try
        {
            var appJs = File.ReadAllText(appJsPath);
            var match = Regex.Match(appJs, "FRONTEND_BUILD\\s*=\\s*[\"'](?<build>[^\"']+)[\"']");
            return match.Success ? match.Groups["build"].Value.Trim() : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }



















    private static IResult Ok(string message, object? data = null)
    {
        return Results.Json(new ApiEnvelope(Msg: message, Data: data));
    }

    private static IResult AuthError(AuthException ex)
    {
        return Results.Json(new
        {
            ok = false,
            msg = ex.Detail,
            detail = ex.Detail,
        }, statusCode: ex.StatusCode);
    }

    private static IResult ScheduleError(ScheduleException ex)
    {
        return Results.Json(new
        {
            ok = false,
            msg = ex.Detail,
            detail = ex.Detail,
        }, statusCode: ex.StatusCode);
    }

    private static IResult CourseError(CourseException ex)
    {
        return Results.Json(new
        {
            ok = false,
            msg = ex.Detail,
            detail = ex.Detail,
            reason = ex.Reason,
            data = ex.ErrorData,
        }, statusCode: ex.StatusCode);
    }

    private static IResult NetworkError(NetworkException ex)
    {
        return Results.Json(new
        {
            ok = false,
            msg = ex.Detail,
            detail = ex.Detail,
        }, statusCode: ex.StatusCode);
    }

    private static IResult TelemetryError(TelemetryException ex)
    {
        return Results.Json(new
        {
            ok = false,
            msg = ex.Detail,
            detail = ex.Detail,
        }, statusCode: ex.StatusCode);
    }

    private static IResult FeedbackError(FeedbackException ex)
    {
        return Results.Json(new
        {
            ok = false,
            msg = ex.Detail,
            detail = ex.Detail,
        }, statusCode: ex.StatusCode);
    }

    private static IResult BlockedError(BlockedAccessException ex)
    {
        return Results.Json(new { detail = ex.Detail }, statusCode: StatusCodes.Status403Forbidden);
    }

    private static string ResolveSessionId(HttpContext context)
    {
        return context.Request.Query.TryGetValue(SessionConstants.SessionQueryName, out var queryValue)
            ? queryValue.ToString().Trim()
            : string.Empty;
    }

    private static string ResolveQueryValue(HttpContext context, string key, string fallback = "")
    {
        return context.Request.Query.TryGetValue(key, out var queryValue)
            ? queryValue.ToString().Trim()
            : fallback;
    }

    private static IReadOnlyList<string> ResolveWarnings(object? value)
    {
        if (value is string)
        {
            return Array.Empty<string>();
        }

        return value is System.Collections.IEnumerable rawValues
            ? rawValues.Cast<object?>()
                .Select(item => (item ?? string.Empty).ToString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : Array.Empty<string>();
    }

    private static async Task EnsureStudentAllowedAsync(NoticeService noticeService, string studentId, string trigger, CancellationToken cancellationToken)
    {
        var resolvedStudentId = (studentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resolvedStudentId))
        {
            return;
        }

        var decision = await noticeService.ResolveStudentAccessDecisionAsync(resolvedStudentId, trigger, cancellationToken).ConfigureAwait(false);
        if (decision.GetValueOrDefault("blocked") is true)
        {
            decision["reason"] = "access-policy-blocked";
            throw new BlockedAccessException(noticeService.BuildBlockedDetail(resolvedStudentId, decision));
        }
    }

    private static async Task EnsureSessionAllowedAsync(AuthService authService, NoticeService noticeService, string sessionId, CancellationToken cancellationToken)
    {
        var resolvedStudentId = authService.ResolveSessionStudentId(sessionId);
        await EnsureStudentAllowedAsync(noticeService, resolvedStudentId, "view", cancellationToken).ConfigureAwait(false);
    }

    private static string GetAvatarContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png",
        };
    }

    private static string ResolveDotNetWorkspaceRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XKE.DotNet8.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }

    private sealed class BlockedAccessException : Exception
    {
        public BlockedAccessException(Dictionary<string, object?> detail)
        {
            Detail = detail;
        }

        public Dictionary<string, object?> Detail { get; }
    }

    private sealed record FrontendDiagnostics(
        string FrontendBuild,
        IReadOnlyDictionary<string, string> StaticAssetHashes);
}
