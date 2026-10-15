using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Network;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NetworkMaintenanceSchedulerTests
{
    [Fact]
    public async Task Scheduler_UsesConfiguredIntervalAndStopsWithoutActiveSession()
    {
        var clock = new TestClock();
        var client = new MaintenanceClient();
        var scheduler = new NetworkMaintenanceScheduler(client, clock);
        var settings = new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 9 };
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, client.Calls);
        Assert.Equal(clock.GetUtcNow().AddMinutes(9), scheduler.State.NextRunAt);
        clock.Advance(TimeSpan.FromMinutes(8));
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, client.Calls);
        clock.Advance(TimeSpan.FromMinutes(1));
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(2, client.Calls);
        Assert.False(client.LastForce);
        await scheduler.RunDueAsync(null, settings, default);
        Assert.False(scheduler.State.Enabled);
        Assert.Null(scheduler.State.NextRunAt);
    }

    [Fact]
    public async Task Scheduler_DisabledSettingsNeverRunAndReenableRunsImmediately()
    {
        var client = new MaintenanceClient();
        var scheduler = new NetworkMaintenanceScheduler(client, new TestClock());
        await scheduler.RunDueAsync("session", new NetworkSettingsDocument { HeartbeatEnabled = false }, default);
        Assert.Equal(0, client.Calls);
        await scheduler.RunDueAsync("session", new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 2 }, default);
        Assert.Equal(1, client.Calls);
        await scheduler.RunDueAsync("session", new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 3 }, default);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task Scheduler_FailureWaitsUntilNextIntervalAndCancellationPropagates()
    {
        var clock = new TestClock();
        var client = new MaintenanceClient { Fail = true };
        var scheduler = new NetworkMaintenanceScheduler(client, clock);
        var settings = new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 7 };
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal("test-failure", scheduler.State.LastError);
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, client.Calls);
        clock.Advance(TimeSpan.FromMinutes(7));
        client.Fail = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduler.RunDueAsync("session", settings, cancellation.Token));
    }

    [Fact]
    public async Task Scheduler_RecordsPortalRejectionAndStillRespectsItsInterval()
    {
        var clock = new TestClock(); var client = new MaintenanceClient { OperationError = "当前IP不允许认证" };
        var scheduler = new NetworkMaintenanceScheduler(client, clock);
        var settings = new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 11 };
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(client.OperationError, scheduler.State.LastError);
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, client.Calls);
        client.OperationError = ""; clock.Advance(TimeSpan.FromMinutes(11));
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(2, client.Calls);
        Assert.Empty(scheduler.State.LastError);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("portal")]
    [InlineData("probe")]
    [InlineData("credentials")]
    public async Task Scheduler_ChangedNetworkConfigurationRunsBeforeTheOldDeadline(string change)
    {
        var clock = new TestClock(); var client = new MaintenanceClient();
        var scheduler = new NetworkMaintenanceScheduler(client, clock);
        var initial = new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 30 };
        await scheduler.RunDueAsync("session", initial, default, 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        var next = new NetworkSettingsDocument
        {
            HeartbeatEnabled = true, HeartbeatIntervalMinutes = 30,
            Provider = change == "provider" ? "telecom" : initial.Provider,
            PortalUrl = change == "portal" ? "http://192.0.2.9/" : initial.PortalUrl,
            ConnectivityCheckUrls = change == "probe" ? ["https://connectivity.invalid/check"] : initial.ConnectivityCheckUrls,
        };
        var revision = change == "credentials" ? 2 : 1;
        await scheduler.RunDueAsync("session", next, default, revision);
        Assert.Equal(2, client.Calls);
        await scheduler.RunDueAsync("session", next, default, revision);
        Assert.Equal(2, client.Calls);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan time) => _now += time;
    }

    private sealed class MaintenanceClient : INetworkMaintenance
    {
        public Task<(string Message, NetworkStatusPayload Payload)> MaintainInBackgroundAsync(string sessionId, CancellationToken cancellationToken = default)
            => MaintainAsync(sessionId, new NetworkActionRequest { Force = false }, cancellationToken);
        public int Calls { get; private set; }
        public bool LastForce { get; private set; }
        public bool Fail { get; set; }
        public string OperationError { get; set; } = "";
        public Task<(string Message, NetworkStatusPayload Payload)> MaintainAsync(string sessionId, NetworkActionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastForce = request.Force;
            if (Fail) throw new IOException("test-failure");
            return Task.FromResult(("test-success", new NetworkStatusPayload("", true, OperationError.Length == 0, false, "", [], [], [], new(), "campus", "", "none", [], "", OperationError)));
        }
    }
}
