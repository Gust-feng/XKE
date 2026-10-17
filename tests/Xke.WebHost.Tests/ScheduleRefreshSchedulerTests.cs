using Xke.Contracts.Models;
using Xke.Core.Schedule;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleRefreshSchedulerTests
{
    [Fact]
    public async Task IntervalScheduler_WaitsForConfiguredTimeAndStopsWhenDisabled()
    {
        var clock = new TestClock();
        var refresh = new RefreshClient();
        var scheduler = new ScheduleRefreshScheduler(refresh, clock);
        var settings = new ScheduleSettingsDocument { AutoRefreshEnabled = true, AutoRefreshMode = "interval", AutoRefreshIntervalHours = 7 };
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(0, refresh.Calls);
        Assert.Equal(clock.GetUtcNow().AddHours(7), scheduler.State.NextRunAt);
        clock.Advance(TimeSpan.FromHours(7));
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, refresh.Calls);
        Assert.Equal(clock.GetUtcNow().AddHours(7), scheduler.State.NextRunAt);
        await scheduler.RunDueAsync(null, settings, default);
        Assert.False(scheduler.State.Enabled);
    }

    [Fact]
    public async Task DailyScheduler_UsesLocalTimeAndPreservesLoginRequiredStatus()
    {
        var clock = new TestClock();
        var refresh = new RefreshClient { Expired = true };
        var scheduler = new ScheduleRefreshScheduler(refresh, clock);
        var settings = new ScheduleSettingsDocument { AutoRefreshEnabled = true, AutoRefreshMode = "daily", AutoRefreshDailyTime = "09:00" };
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 1, 0, 0, TimeSpan.Zero), scheduler.State.NextRunAt);
        clock.Advance(TimeSpan.FromHours(1));
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, refresh.Calls);
        Assert.True(scheduler.State.RequiresLogin);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 1, 0, 0, TimeSpan.Zero), scheduler.State.NextRunAt);
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Equal(1, refresh.Calls);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("test-local", TimeSpan.FromHours(8), "test-local", "test-local");
        public void Advance(TimeSpan time) => _now += time;
    }

    private sealed class RefreshClient : IScheduleRefresh
    {
        public int Calls { get; private set; }
        public bool Expired { get; set; }
        public Task RefreshInBackgroundAsync(string sessionId, CancellationToken cancellationToken)
        {
            Calls++;
            if (Expired) throw new ScheduleException(401, "test-session-expired");
            return Task.CompletedTask;
        }
    }
}
