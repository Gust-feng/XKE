using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Responses;
using Xke.Core.Desktop;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class XkeWebHostServiceTests
{
    [Fact]
    public async Task HostService_StartsAndServesShellEndpoints()
    {
        using var webRoot = new TemporaryWebRoot();
        await using var host = await WebHostTestServer.StartAsync(webRoot, hostMode: "test-shell");
        var client = host.Client;

        var health = await client.GetFromJsonAsync<HostHealthResponse>("/api/health");
        Assert.NotNull(health);
        Assert.Equal("ok", health!.Status);
        Assert.True(health.WebRootFound);
        Assert.Equal("test-shell-build", health.FrontendBuild);
        Assert.NotNull(health.StaticAssetHashes);
        Assert.Contains("index.html", health.StaticAssetHashes!.Keys);
        Assert.Contains("assets/app.js", health.StaticAssetHashes.Keys);
        Assert.Contains("assets/modules/profile.js", health.StaticAssetHashes.Keys);

        var config = await client.GetFromJsonAsync<HostConfigResponse>("/api/config");
        Assert.NotNull(config);
        Assert.Equal("auto", config!.PreloginMode);
        Assert.Equal("dotnet8-test", config.Development.Channel);
        Assert.Equal("test-shell", config.Development.HostMode);

        var index = await client.GetStringAsync("/");
        Assert.Contains("XKE Test Shell", index);

        var asset = await client.GetStringAsync("/assets/app.js");
        Assert.Contains("shell-ready", asset);

        var missingApi = await client.GetAsync("/api/missing");
        Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
    }

    [Fact]
    public async Task HostService_ViteHashedAssetsAndBuildMetadataAreDiagnosedAndServed()
    {
        using var web = new TemporaryDirectory("xke-vite-test");
        Directory.CreateDirectory(Path.Combine(web.RootPath, "assets", "chunks"));
        File.WriteAllText(Path.Combine(web.RootPath, "index.html"), """
            <html><script type="module" src="./assets/index-ABC123.js"></script><link rel="stylesheet" href="./assets/index-DEF456.css"></html>
            """);
        File.WriteAllText(Path.Combine(web.RootPath, "frontend-build.json"), "{\"build\":\"vite-test-build\"}");
        File.WriteAllText(Path.Combine(web.RootPath, "assets", "index-ABC123.js"), "import './chunks/grid-XYZ.js';");
        File.WriteAllText(Path.Combine(web.RootPath, "assets", "index-DEF456.css"), "body { color: red; }");
        File.WriteAllText(Path.Combine(web.RootPath, "assets", "chunks", "grid-XYZ.js"), "export const grid = true;");
        await using var host = await WebHostTestServer.StartAsync(web.RootPath);
        var health = (await host.Client.GetFromJsonAsync<HostHealthResponse>("/api/health"))!;
        Assert.Equal("vite-test-build", health.FrontendBuild);
        Assert.Equal(new[] { "assets/chunks/grid-XYZ.js", "assets/index-ABC123.js", "assets/index-DEF456.css", "frontend-build.json", "index.html" },
            health.StaticAssetHashes!.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in health.StaticAssetHashes)
        {
            var bytes = File.ReadAllBytes(Path.Combine(web.RootPath, pair.Key));
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), pair.Value);
        }
        var startup = new Uri(new Uri(host.LaunchResult.BaseUrl), "/?xke_frontend_build=vite-test-build");
        Assert.Contains("./assets/index-ABC123.js", await host.Client.GetStringAsync(startup));
        var js = await host.Client.GetAsync(new Uri(startup, "./assets/index-ABC123.js"));
        Assert.Equal("text/javascript", js.Content.Headers.ContentType!.MediaType);
        Assert.Contains("grid-XYZ.js", await js.Content.ReadAsStringAsync());
        Assert.Contains("grid = true", await host.Client.GetStringAsync("/assets/chunks/grid-XYZ.js"));
        using var css = await host.Client.GetAsync(new Uri(startup, "./assets/index-DEF456.css"));
        Assert.Equal("text/css", css.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task HostService_MetadataTakesPrecedenceOverLegacyBuildAndInvalidMetadataFallsBack()
    {
        using var web = new TemporaryWebRoot();
        var metadataPath = Path.Combine(web.RootPath, "frontend-build.json");
        File.WriteAllText(metadataPath, "{\"build\":\"new-build\"}");
        await using var host = await WebHostTestServer.StartAsync(web);
        var health = (await host.Client.GetFromJsonAsync<HostHealthResponse>("/api/health"))!;
        Assert.Equal("new-build", health.FrontendBuild);
        File.WriteAllText(metadataPath, "invalid");
        health = (await host.Client.GetFromJsonAsync<HostHealthResponse>("/api/health"))!;
        Assert.Equal("test-shell-build", health.FrontendBuild);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-json")]
    [InlineData("{\"build\":42}")]
    [InlineData("{\"build\":\" \"}")]
    public async Task HostService_ViteWithoutValidMetadataUsesStableAssetFingerprint(string? metadata)
    {
        using var web = new TemporaryDirectory("xke-vite-test");
        Directory.CreateDirectory(Path.Combine(web.RootPath, "assets"));
        File.WriteAllText(Path.Combine(web.RootPath, "index.html"), "<script src='./assets/index-hash.js'></script>");
        File.WriteAllText(Path.Combine(web.RootPath, "assets", "index-hash.js"), "console.log('vite');");
        if (metadata is not null) File.WriteAllText(Path.Combine(web.RootPath, "frontend-build.json"), metadata);
        await using var host = await WebHostTestServer.StartAsync(web.RootPath);
        var first = (await host.Client.GetFromJsonAsync<HostHealthResponse>("/api/health"))!;
        var second = (await host.Client.GetFromJsonAsync<HostHealthResponse>("/api/health"))!;
        Assert.StartsWith("sha256-", first.FrontendBuild);
        Assert.Equal(first.FrontendBuild, second.FrontendBuild);
        Assert.Contains("assets/index-hash.js", first.StaticAssetHashes!.Keys);
        File.WriteAllText(Path.Combine(web.RootPath, "assets", "index-hash.js"), "console.log('changed');");
        var changed = (await host.Client.GetFromJsonAsync<HostHealthResponse>("/api/health"))!;
        Assert.NotEqual(first.FrontendBuild, changed.FrontendBuild);
    }

    [Fact]
    public void WebRootResolver_FindsPackagedWebAndDevelopmentViteBuild()
    {
        using var runtime = new TemporaryDirectory("xke-runtime-path-test");
        Directory.CreateDirectory(Path.Combine(runtime.RootPath, "web"));
        File.WriteAllText(Path.Combine(runtime.RootPath, "web", "index.html"), "packaged");
        Assert.Equal(Path.Combine(runtime.RootPath, "web"), Xke.Core.Runtime.XkePathResolver.FindWebRoot(startDirectory: runtime.RootPath));
        Directory.CreateDirectory(Path.Combine(runtime.RootPath, "frontend", "dist"));
        File.WriteAllText(Path.Combine(runtime.RootPath, "frontend", "dist", "index.html"), "vite");
        Assert.Equal(Path.Combine(runtime.RootPath, "frontend", "dist"), Xke.Core.Runtime.XkePathResolver.FindWebRoot(startDirectory: runtime.RootPath));
        Assert.Equal(Path.Combine(runtime.RootPath, "web"), Xke.Core.Runtime.XkePathResolver.FindWebRoot(explicitWebRootPath: Path.Combine(runtime.RootPath, "web")));
    }

    [Fact]
    public async Task HostService_StillStartsWhenWebRootIsMissing()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var missingHost = await WebHostTestServer.StartAsync(missingRoot, hostMode: "test-shell");
        var client = missingHost.Client;

        var rootResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, rootResponse.StatusCode);

        using var document = JsonDocument.Parse(await rootResponse.Content.ReadAsStringAsync());
        Assert.Equal("web root not found", document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task HostService_FallbackServesOnlyFilesInsideWebRoot()
    {
        using var webRoot = new TemporaryWebRoot();
        var siblingRoot = $"{webRoot.RootPath}-other";
        Directory.CreateDirectory(siblingRoot);
        File.WriteAllText(Path.Combine(siblingRoot, "leak"), "outside-web-root");
        try
        {
            await using var host = await WebHostTestServer.StartAsync(webRoot, hostMode: "fallback-boundary-test");
            var client = host.Client;

            var rootFile = await client.GetStringAsync("/shell-static");
            Assert.Equal("test-shell-static", rootFile);

            var missingFile = await client.GetStringAsync("/missing-file");
            Assert.Contains("XKE Test Shell", missingFile);

            var escapedTraversal = $"/%2e%2e/{Path.GetFileName(siblingRoot)}/leak";
            var traversalResponse = await client.GetAsync(escapedTraversal);
            Assert.Equal(HttpStatusCode.OK, traversalResponse.StatusCode);
            Assert.DoesNotContain("outside-web-root", await traversalResponse.Content.ReadAsStringAsync());
        }
        finally
        {
            if (Directory.Exists(siblingRoot))
            {
                Directory.Delete(siblingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HostService_DesktopRoutes_ExposeSettingsAndNoopActivateDiagnostics()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory();
        await using var host = await WebHostTestServer.StartAsync(webRoot, dataRoot, hostMode: "desktop-route-test");
        var client = host.Client;

        var settingsResponse = await client.GetAsync("/api/desktop/settings");
        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);

        using var settingsDocument = JsonDocument.Parse(await settingsResponse.Content.ReadAsStringAsync());
        var settingsPayload = settingsDocument.RootElement.GetProperty("data");
        Assert.False(settingsPayload.GetProperty("settings").GetProperty("launch_at_startup").GetBoolean());
        Assert.False(settingsPayload.GetProperty("settings").GetProperty("silent_startup").GetBoolean());
        Assert.True(settingsPayload.GetProperty("settings").GetProperty("close_to_tray").GetBoolean());
        Assert.Equal(JsonValueKind.Null, settingsPayload.GetProperty("startup_registered").ValueKind);

        var activateResponse = await client.PostAsJsonAsync("/api/desktop/activate", new { });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        using var activateDocument = JsonDocument.Parse(await activateResponse.Content.ReadAsStringAsync());
        var activatePayload = activateDocument.RootElement.GetProperty("data");
        Assert.False(activatePayload.GetProperty("activated").GetBoolean());
        Assert.False(activatePayload.GetProperty("visible").GetBoolean());
        Assert.Equal("desktop-window-unavailable", activatePayload.GetProperty("window_state").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task HostService_DesktopActivateRoute_UsesConfiguredWindowController()
    {
        using var webRoot = new TemporaryWebRoot();
        var desktopController = new FakeDesktopWindowController();
        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            hostMode: "desktop-route-test",
            desktopWindowController: desktopController);
        var client = host.Client;

        var activateResponse = await client.PostAsJsonAsync("/api/desktop/activate", new { });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        using var activateDocument = JsonDocument.Parse(await activateResponse.Content.ReadAsStringAsync());
        var activatePayload = activateDocument.RootElement.GetProperty("data");
        Assert.True(activatePayload.GetProperty("activated").GetBoolean());
        Assert.True(activatePayload.GetProperty("visible").GetBoolean());
        Assert.Equal("normal", activatePayload.GetProperty("window_state").GetProperty("state").GetString());
        Assert.Equal(1, desktopController.CallCount);
    }

    [Fact]
    public async Task HostService_DesktopWebViewMode_ReusesStableLoopbackPortAcrossRestarts()
    {
        using var dataRoot = new TemporaryDirectory();
        using var webRoot = new TemporaryWebRoot();
        var firstService = new XkeWebHostService(new XkeWebHostOptions
        {
            ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(),
            AuthDataRootPath = dataRoot.RootPath,
            EnableBackgroundTasks = false,
            HostMode = "desktop-webview",
            RuntimeChannel = "dotnet8-test",
            WebRootPath = webRoot.RootPath,
        });

        var firstLaunch = await firstService.StartAsync();
        var firstPort = new Uri(firstLaunch.BaseUrl).Port;
        await firstService.DisposeAsync();

        var secondService = new XkeWebHostService(new XkeWebHostOptions
        {
            AuthDataRootPath = dataRoot.RootPath,
            EnableBackgroundTasks = false,
            HostMode = "desktop-webview",
            RuntimeChannel = "dotnet8-test",
            WebRootPath = webRoot.RootPath,
        });

        var secondLaunch = await secondService.StartAsync();
        var secondPort = new Uri(secondLaunch.BaseUrl).Port;
        await secondService.DisposeAsync();

        Assert.Equal(firstPort, secondPort);
        Assert.Equal(38443, firstPort);
    }

    [Fact]
    public async Task HostService_ServesLegacyFaviconAndManifestFromProvidedFrontendRoot()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        File.WriteAllText(Path.Combine(web.RootPath, "manifest.webmanifest"), "{\"name\":\"Test frontend\"}");
        await using var host = await WebHostTestServer.StartAsync(web, data);
        using var icon = await host.Client.GetAsync("/favicon.ico");
        using var manifest = await host.Client.GetAsync("/manifest.webmanifest");
        Assert.Equal(HttpStatusCode.OK, icon.StatusCode);
        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Equal("image/x-icon", icon.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/manifest+json", manifest.Content.Headers.ContentType?.MediaType);
    }

    private sealed class FakeDesktopWindowController : IDesktopWindowController
    {
        public int CallCount { get; private set; }

        public DesktopActivatePayload Activate()
        {
            CallCount++;
            return new DesktopActivatePayload(
                Activated: true,
                Visible: true,
                WindowState: new Dictionary<string, object?>
                {
                    ["available"] = true,
                    ["state"] = "normal",
                });
        }
    }
}
