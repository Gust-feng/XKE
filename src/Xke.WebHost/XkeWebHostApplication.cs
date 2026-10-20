using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xke.Contracts.Api;
using Xke.Contracts.Enums;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Core.Desktop;
using Xke.Core.Runtime;
using Xke.Core.Schedule;
using Xke.Core.Network;
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
        builder.Services.AddSingleton<TraditionalJwSessionRuntimeStore>();
        builder.Services.AddSingleton(services => new AutomaticCaptchaLogin(services.GetRequiredService<XkeDataRoot>(), hostOptions.CaptchaRecognizer));
        builder.Services.AddSingleton<IJwScheduleClient>(_ =>
            hostOptions.JwScheduleClient ?? new NativeJwScheduleClient(
                _.GetRequiredService<TraditionalJwSessionRuntimeStore>(),
                _.GetRequiredService<XkeDataRoot>(), _.GetRequiredService<AutomaticCaptchaLogin>(), hostOptions.JwLoginProtocolFactory));
        var scheduleApiOptions = hostOptions.ScheduleApiOptions ?? ScheduleApiOptions.Load(dotNetRoot);
        builder.Services.AddSingleton(scheduleApiOptions);
        var snapshotClient = scheduleApiOptions.Provider == "protocol"
            ? builder.Services.AddHttpClient<IScheduleSnapshotSource, ProtocolScheduleApiClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            : builder.Services.AddHttpClient<IScheduleSnapshotSource, PersonalScheduleApiClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        snapshotClient.ConfigurePrimaryHttpMessageHandler(services => ScheduleApiTransport.CreateHandler(services.GetRequiredService<ScheduleApiOptions>()));
        if (hostOptions.ScheduleSnapshotSource is not null)
            builder.Services.AddSingleton<IScheduleSnapshotSource>(hostOptions.ScheduleSnapshotSource);
        builder.Services.AddSingleton(_ =>
            hostOptions.NetworkHttpMessageHandler is null
                ? new HttpClient(NetworkHttpTransport.CreateHandler())
                : new HttpClient(hostOptions.NetworkHttpMessageHandler, disposeHandler: false));
        builder.Services.AddSingleton<INetworkCommandExecutor>(_ =>
            hostOptions.NetworkCommandExecutor ?? new ProcessNetworkCommandExecutor());
        builder.Services.AddSingleton<IDesktopStartupRegistrar>(_ =>
            hostOptions.DesktopStartupRegistrar ?? new NoopDesktopStartupRegistrar());
        builder.Services.AddSingleton<DesktopSettingsService>();
        builder.Services.AddSingleton(hostOptions.AppUpdates ?? new Xke.Core.Releases.AppUpdateService(
            new Xke.Core.Releases.DisabledAppUpdateBackend(), TimeProvider.System));
        builder.Services.AddSingleton<IDesktopNotifier>(_ => hostOptions.DesktopNotifier ?? new NoopDesktopNotifier());
        builder.Services.AddSingleton<IDesktopWindowController>(_ =>
            hostOptions.DesktopWindowController ?? new NoopDesktopWindowController());
        builder.Services.AddSingleton(services => new AuthService(
            services.GetRequiredService<XkeDataRoot>(),
            services.GetRequiredService<ScheduleService>(),
            services.GetRequiredService<TraditionalJwSessionRuntimeStore>(),
            hostOptions.JwLoginProtocolFactory, services.GetRequiredService<AutomaticCaptchaLogin>()));
        builder.Services.AddSingleton<JwCaptchaChallengeStore>();
        builder.Services.AddSingleton<IJwCaptchaSolver, ManualCaptchaSolver>();
        builder.Services.AddSingleton(_ => new ScheduleService(
            _.GetRequiredService<XkeDataRoot>(),
            _.GetRequiredService<IJwScheduleClient>(),
            _.GetRequiredService<JwCaptchaChallengeStore>(),
            _.GetRequiredService<IJwCaptchaSolver>(),
            _.GetRequiredService<IScheduleSnapshotSource>(),
            () => _.GetRequiredService<AuthService>().ActiveSessionId));
        builder.Services.AddSingleton<IScheduleRefresh>(services => services.GetRequiredService<ScheduleService>());
        builder.Services.AddSingleton(services => new ScheduleRefreshScheduler(
            services.GetRequiredService<IScheduleRefresh>(), services.GetRequiredService<TimeProvider>(),
            session => services.GetRequiredService<TraditionalJwSessionRuntimeStore>().TryGet(session, out _)));
        builder.Services.AddSingleton(services => new LessonReminderScheduler(
            services.GetRequiredService<XkeDataRoot>(), services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<IDesktopNotifier>(), () => services.GetRequiredService<AuthService>().ActiveSessionId));
        builder.Services.AddSingleton(services =>
        {
            var auth = services.GetRequiredService<AuthService>();
            return new NetworkService(services.GetRequiredService<XkeDataRoot>(), services.GetRequiredService<HttpClient>(),
                services.GetRequiredService<INetworkCommandExecutor>(), () => auth.ActiveSessionId);
        });
        builder.Services.AddSingleton(hostOptions.CampusSelfServiceOptions ?? CampusSelfServiceOptions.Load(dotNetRoot));
        builder.Services.AddSingleton(services => new CampusSelfServiceSummaryService(
            services.GetRequiredService<XkeDataRoot>(),
            services.GetRequiredService<CampusSelfServiceOptions>(),
            services.GetRequiredService<TimeProvider>(),
            hostOptions.CampusSelfServiceClientFactory));
        builder.Services.AddSingleton<INetworkMaintenance>(services => services.GetRequiredService<NetworkService>());
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<NetworkMaintenanceScheduler>();
        if (hostOptions.EnableBackgroundTasks)
        {
            builder.Services.AddHostedService<PersonalRuntimeWorker>();
            builder.Services.AddHostedService<AppUpdateWorker>();
        }
        builder.Services.AddCors(cors =>
        {
            cors.AddPolicy("ExplicitFrontend", policy =>
            {
                // Same-origin requests need no CORS. Cross-origin development is explicit.
                policy.WithOrigins(hostOptions.AllowedFrontendOrigins.ToArray());
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
            });
        });

        var app = builder.Build();

        app.Use(HandleApiErrors);
        app.Use(async (context, next) =>
        {
            if (LocalApiAccessPolicy.ValidateRequest(context, hostOptions.HostMode,
                hostOptions.AllowedFrontendOrigins, hostOptions.DesktopRequestToken))
                await next();
        });
        app.UseWhen(context => context.Request.Headers.TryGetValue("Origin", out var origins)
            && !LocalApiAccessPolicy.IsAllowedOrigin(context.Request, origins.ToString(), Array.Empty<string>()),
            branch => branch.UseCors("ExplicitFrontend"));
        app.Use(ApplySessionHeaderCompatibility);
        app.Use(ApplyNoCacheHeaders);

        ConfigureStaticFiles(app, resolvedPaths);
        MapShellRoutes(app);

        return app;
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

        foreach (var asset in new[] { (Name: "favicon.ico", Type: "image/x-icon"), (Name: "manifest.webmanifest", Type: "application/manifest+json") })
        {
            var file = Path.Combine(resolvedPaths.WebRootPath, asset.Name);
            var contentType = asset.Type;
            app.MapGet("/" + asset.Name, () => File.Exists(file) ? Results.File(file, contentType) : Results.NotFound());
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
        app.MapGet("/api/health", (HttpContext context, XkeResolvedPaths paths, AuthService authService, Xke.Core.Releases.AppUpdateService updates) =>
        {
            var frontendDiagnostics = BuildFrontendDiagnostics(paths);
            return Results.Json(new HostHealthResponse(
                Status: "ok",
                Sessions: authService.SessionCount,
                WebRootFound: paths.HasWebRoot,
                WebRoot: paths.WebRootPath ?? string.Empty,
                BaseUrl: BuildBaseUrl(context),
                FrontendBuild: frontendDiagnostics.FrontendBuild,
                StaticAssetHashes: frontendDiagnostics.StaticAssetHashes,
                AppVersion: Xke.Core.Releases.AppRelease.Version,
                UpdateSupported: updates.IsInstalled));
        });

        app.MapGet("/api/config", (HttpContext context, XkeResolvedPaths paths, XkeWebHostOptions options) =>
            Results.Json(new HostConfigResponse(
                PreloginMode: AppOptions.DefaultPreloginMode,
                Development: new HostDevelopmentResponse(
                    Channel: options.RuntimeChannel,
                    HostMode: options.HostMode,
                    BaseUrl: BuildBaseUrl(context),
                    WebRoot: paths.WebRootPath ?? string.Empty,
                    WebRootFound: paths.HasWebRoot),
                Defaults: AuthSettingsCatalog.CreateDefault())));

        MapAppUpdateRoutes(app);
        MapDesktopRoutes(app);
        MapAuthRoutes(app);
        MapScheduleRoutes(app);
        MapNetworkRoutes(app);

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
                context.Response.ContentType = "text/html; charset=utf-8";
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
        var relativePaths = new List<string> { "index.html", "frontend-build.json" };
        var assetsRoot = Path.Combine(paths.WebRootPath, "assets");
        if (Directory.Exists(assetsRoot))
        {
            try
            {
                relativePaths.AddRange(Directory.EnumerateFiles(assetsRoot, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = true,
                }).Select(path => Path.GetRelativePath(paths.WebRootPath, path).Replace('\\', '/')));
            }
            catch (IOException) { /* A build may replace assets during a health request. */ }
            catch (UnauthorizedAccessException) { }
        }

        foreach (var relativePath in relativePaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            var absolutePath = Path.Combine(paths.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(absolutePath);
                var hash = SHA256.HashData(stream);
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

        var build = ResolveFrontendBuild(paths.WebRootPath);
        if (string.IsNullOrWhiteSpace(build) && assetHashes.Count > 0)
        {
            var fingerprint = string.Join('\n', assetHashes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{pair.Value}"));
            build = "sha256-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant();
        }
        return new FrontendDiagnostics(build, assetHashes);
    }

    private static string ResolveFrontendBuild(string webRootPath)
    {
        var metadataPath = Path.Combine(webRootPath, "frontend-build.json");
        try
        {
            if (File.Exists(metadataPath))
            {
                using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
                if (metadata.RootElement.ValueKind == JsonValueKind.Object
                    && metadata.RootElement.TryGetProperty("build", out var build)
                    && build.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(build.GetString()))
                    return build.GetString()!.Trim();
            }
        }
        catch (JsonException) { /* Invalid optional metadata must not break the host. */ }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

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

    private static async Task HandleApiErrors(HttpContext context, Func<Task> next)
    {
        try { await next(); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
        {
            var status = ex switch
            {
                AuthException auth => auth.StatusCode,
                ScheduleException schedule => schedule.StatusCode,
                NetworkException network => network.StatusCode,
                BadHttpRequestException request => request.StatusCode,
                _ => StatusCodes.Status500InternalServerError,
            };
            var knownError = ex is AuthException or ScheduleException or NetworkException or BadHttpRequestException;
            if (!knownError)
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Xke.WebHost.Api")
                    .LogError(ex, "API request failed: {Path}", context.Request.Path.Value);
            }
            await Error(status, knownError ? ex.Message : "操作未完成，请重试").ExecuteAsync(context);
        }
    }

    private static IResult Error(int status, string detail)
    {
        return Results.Json(new ApiErrorResponse(detail, detail), statusCode: status);
    }

    private static IResult Ok(string message, object? data = null)
    {
        return Results.Json(new ApiEnvelope(Msg: message, Data: data));
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

    private sealed record FrontendDiagnostics(
        string FrontendBuild,
        IReadOnlyDictionary<string, string> StaticAssetHashes);
}
