using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Core.Network;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class PersonalRuntimeWebHostTests
{
    [Fact]
    public async Task CoreHost_ExposesLocalDefaultsAndReturns404ForRetiredRoutes()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        await using var host = await WebHostTestServer.StartAsync(web, data);
        using var config = JsonDocument.Parse(await host.Client.GetStringAsync("/api/config"));
        Assert.True(config.RootElement.TryGetProperty("defaults", out _));
        Assert.False(config.RootElement.TryGetProperty("remote", out _));
        Assert.False(config.RootElement.TryGetProperty("beta", out _));
        foreach (var endpoint in new[] { "/api/updates/feed", "/api/update/status", "/api/course/status" })
        {
            using var response = await host.Client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        foreach (var endpoint in new[] { "/api/telemetry/report", "/api/feedback/report", "/api/course/connect" })
        {
            using var response = await host.Client.PostAsJsonAsync(endpoint, new { });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task BackgroundHost_RestoresLocalWorkspaceWithoutWebRootAndLogoutClearsActiveSession()
    {
        using var data = new TemporaryDirectory();
        var root = new XkeDataRoot(data.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001", StudentId = "20260001", Password = "test-password", TrustedLoginToken = "test-trust",
        });
        var student = root.ForStudent("20260001");
        student.SaveProfile(new StudentProfileDocument { StudentId = "20260001", Name = "测试同学" });
        student.SavePreferences(AuthSettingsCatalog.Apply(AuthSettingsCatalog.CreateDefault(), new PreferencesRequest
        {
            Network = new NetworkSettingsRequest { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 9 },
        }));
        student.Schedules().SaveSourceSchedule(new Dictionary<string, object?>
        {
            ["source_key"] = "jw_schedule", ["week_label"] = "第8周",
            ["rows"] = new List<Dictionary<string, object?>> { new() { ["course_name"] = "测试课程", ["weekday"] = 1, ["section"] = "1-2" } },
        }, new Dictionary<string, object?>(), "jw_schedule", "第8周");
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            ScheduleApiOptions = new Xke.Core.Schedule.ScheduleApiOptions(),
            WebRootPath = Path.Combine(data.RootPath, "missing-web"), AuthDataRootPath = data.RootPath,
            Urls = new[] { "http://127.0.0.1:0" },
            NetworkCommandExecutor = new OfflineCommands(),
            NetworkHttpMessageHandler = new OfflineHandler(),
            JwLoginProtocolFactory = (_, _) => throw new InvalidOperationException("Local login must not call JW"),
        });
        await app.StartAsync();
        var auth = app.Services.GetRequiredService<AuthService>();
        for (var attempt = 0; attempt < 100 && auth.ActiveSessionId is null; attempt++) await Task.Delay(10);
        Assert.NotNull(auth.ActiveSessionId);
        var active = auth.ActiveSessionId!;
        Assert.Equal(active, auth.DesktopBootstrap().Payload.SessionId);
        var scheduler = app.Services.GetRequiredService<NetworkMaintenanceScheduler>();
        for (var attempt = 0; attempt < 100 && scheduler.State.LastRunAt is null; attempt++) await Task.Delay(10);
        Assert.True(scheduler.State.Enabled);
        Assert.NotNull(scheduler.State.LastRunAt);
        var localLogin = await auth.BeginProtocolLoginAsync(new LoginRequest { User = "20260001", Password = "test-password" });
        Assert.IsType<Xke.Contracts.Responses.AuthPayload>(localLogin.Payload);
        active = auth.ActiveSessionId!;
        Assert.False(app.Services.GetRequiredService<Xke.Core.Runtime.XkeResolvedPaths>().HasWebRoot);
        auth.Logout(active);
        Assert.Null(auth.ActiveSessionId);
        await app.StopAsync();
    }
    private sealed class OfflineCommands : INetworkCommandExecutor
    {
        public Task<NetworkCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult(new NetworkCommandResult(1, string.Empty, "test-offline"));
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

}
