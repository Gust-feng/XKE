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
