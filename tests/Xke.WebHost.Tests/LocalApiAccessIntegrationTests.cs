using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xke.Contracts.Models;
using Xke.Core.Auth;
using Xke.Core.Schedule;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class LocalApiAccessIntegrationTests
{
    [Fact]
    public async Task ForeignLocalOriginCannotReadOrMutate_AndNativeBootstrapNeedsCapability()
    {
        using var temp = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        const string token = "synthetic-local-capability";
        await using var service = new XkeWebHostService(new() { HostMode = "desktop-webview", DesktopRequestToken = token,
            AuthDataRootPath = temp.RootPath, WebRootPath = web.RootPath, EnableBackgroundTasks = false,
            ScheduleApiOptions = new(), Urls = ["http://127.0.0.1:0"] });
        var launch = await service.StartAsync(); using var client = new HttpClient { BaseAddress = new(launch.BaseUrl) };
        using var health = await client.GetAsync("/api/health"); Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var foreign = new HttpRequestMessage(HttpMethod.Get, "/api/config"); foreign.Headers.Add("Origin", "http://localhost:5182");
        using var rejected = await client.SendAsync(foreign); Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        using var rebound = new HttpRequestMessage(HttpMethod.Get, "/api/health"); rebound.Headers.Host = "foreign.example";
        using var reboundResponse = await client.SendAsync(rebound); Assert.Equal(HttpStatusCode.Forbidden, reboundResponse.StatusCode);
        using var noProof = await client.PostAsJsonAsync("/api/desktop/auth-bootstrap", new { }); Assert.Equal(HttpStatusCode.Forbidden, noProof.StatusCode);
        using var noSettings = await client.PostAsJsonAsync("/api/desktop/settings", new { launch_at_startup = true }); Assert.Equal(HttpStatusCode.Forbidden, noSettings.StatusCode);
        client.DefaultRequestHeaders.Add(LocalApiAccessPolicy.DesktopTokenHeaderName, token);
        using var authorized = await client.PostAsJsonAsync("/api/desktop/auth-bootstrap", new { });
        // No account was seeded: getting through to the endpoint gives the existing 404, not a fake success.
        Assert.Equal(HttpStatusCode.NotFound, authorized.StatusCode);
        using var config = await client.GetAsync("/api/config"); var body = await config.Content.ReadAsStringAsync(); Assert.DoesNotContain(token, body);
    }

    [Fact]
    public async Task ExplicitDevelopmentOrigin_AllowsPreflightButDoesNotGrantDesktopTrust()
    {
        using var temp = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        const string origin = "http://127.0.0.1:5182";
        await using var service = new XkeWebHostService(new() { HostMode = "desktop-webview", DesktopRequestToken = "synthetic",
            AuthDataRootPath = temp.RootPath, WebRootPath = web.RootPath, EnableBackgroundTasks = false,
            ScheduleApiOptions = new(), AllowedFrontendOrigins = [origin], Urls = ["http://127.0.0.1:0"] });
        var launch = await service.StartAsync(); using var client = new HttpClient { BaseAddress = new(launch.BaseUrl) };
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/desktop/auth-bootstrap");
        preflight.Headers.Add("Origin", origin); preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var response = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/desktop/auth-bootstrap") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Origin", origin); using var forbidden = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
