using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleMaintenanceReliabilityTests
{
    [Theory]
    [InlineData("logout")]
    [InlineData("switch")]
    [InlineData("disable")]
    [InlineData("mode")]
    [InlineData("term")]
    [InlineData("interval")]
    public async Task InFlightSnapshot_DoesNotSaveAfterContextChanges(string change)
    {
        using var f = new Fixture();
        var pending = f.Service.RefreshInBackgroundAsync(f.Session, default);
        await f.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if (change == "logout") f.Root.Sessions().Remove(f.Session);
        else if (change == "switch") f.Active = "another-session";
        else f.Student.SavePreferences(Patch(f.Settings, change));
        f.Source.Release.SetResult();
        var error = await Assert.ThrowsAsync<ScheduleException>(() => pending);
        Assert.Equal(409, error.StatusCode);
        Assert.Null(f.Student.Schedules().LoadLatestSourceSchedule(["jw_schedule"]));
        Assert.Equal(0, f.Fake.CookieCalls);
    }

    [Fact]
    public async Task OldSourceFailureAfterLogout_IsContextCancellationNotAnAuthFailure()
    {
        using var f = new Fixture();
        var pending = f.Service.RefreshInBackgroundAsync(f.Session, default);
        await f.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        f.Root.Sessions().Remove(f.Session);
        f.Fake.CookieException = new JwScheduleException("synthetic-old-failure", 401);
        f.Source.Release.SetResult();
        Assert.Equal(409, (await Assert.ThrowsAsync<ScheduleException>(() => pending)).StatusCode);
    }

    [Fact]
    public async Task DisabledBeforeStart_DoesNotContactSource_AndManualRefreshStillWorks()
    {
        using var f = new Fixture();
        f.Student.SavePreferences(new() { Schedule = new() { AutoRefreshEnabled = false } });
        Assert.Equal(409, (await Assert.ThrowsAsync<ScheduleException>(() => f.Service.RefreshInBackgroundAsync(f.Session, default))).StatusCode);
        Assert.False(f.Source.Entered.Task.IsCompleted);
        f.Source.Release.SetResult();
        var result = await f.Service.FetchPreferredAsync(f.Session, new ScheduleRequest());
        Assert.NotNull(result.Payload);
    }

    [Fact]
    public async Task CancelledSnapshot_ReleasesGateForNextValidRefresh()
    {
        using var f = new Fixture(); using var cancellation = new CancellationTokenSource();
        var pending = f.Service.RefreshInBackgroundAsync(f.Session, cancellation.Token);
        await f.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        f.Source.Release.SetResult(); await f.Service.RefreshInBackgroundAsync(f.Session, default);
        Assert.NotNull(f.Student.Schedules().LoadLatestSourceSchedule(["jw_schedule"]));
    }

    [Fact]
    public async Task Scheduler_ContextCancellationIsNotFailureOrSuccessfulRun()
    {
        var clock = new Clock(); var refresh = new Invalidated(); var scheduler = new ScheduleRefreshScheduler(refresh, clock);
        var settings = new ScheduleSettingsDocument { AutoRefreshEnabled = true, AutoRefreshMode = "interval", AutoRefreshIntervalHours = 1 };
        await scheduler.RunDueAsync("session", settings, default); clock.Now += TimeSpan.FromHours(1);
        await scheduler.RunDueAsync("session", settings, default);
        Assert.Empty(scheduler.State.LastError); Assert.False(scheduler.State.RequiresLogin); Assert.Null(scheduler.State.LastRunAt);
        await scheduler.RunDueAsync("session", settings, default); Assert.NotNull(scheduler.State.NextRunAt);
    }

    private static AppSettingsDocument Patch(AppSettingsDocument settings, string change)
        => AuthSettingsCatalog.Apply(settings, new() { Schedule = new() {
            AutoRefreshEnabled = change == "disable" ? false : null,
            AutoRefreshMode = change == "mode" ? "interval" : null,
            AcademicTerm = change == "term" ? "2026-2027-2" : null,
            AutoRefreshIntervalHours = change == "interval" ? 7 : null,
        } });

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory _temp = new();
        public XkeDataRoot Root { get; }
        public XkeStudentStore Student { get; }
        public string Session { get; }
        public string? Active;
        public AppSettingsDocument Settings { get; } = new() { Schedule = new() { AutoRefreshEnabled = true } };
        public DefaultFakeJwScheduleClient Fake { get; } = new();
        public Source Source { get; }
        public ScheduleService Service { get; }
        public Fixture()
        {
            Root = new(_temp.RootPath); Student = Root.ForStudent("SCHEDULE");
            Root.Credentials().Save(new() { User = "synthetic", StudentId = Student.StudentId, Password = "synthetic-only" });
            Student.SavePreferences(Settings);
            Session = Root.Sessions().Create("synthetic", Student.StudentId, "auto").SessionId; Active = Session;
            Source = new(Fake);
            Service = new(Root, Fake, new(), new ManualCaptchaSolver(), snapshotSource: Source, activeSession: () => Active);
        }
        public void Dispose() { Service.Dispose(); _temp.Dispose(); }
    }
    private sealed class Source(DefaultFakeJwScheduleClient fake) : IScheduleSnapshotSource
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ScheduleSnapshotAttempt> FetchAsync(string student, ScheduleRequest request, CancellationToken token)
        {
            Entered.TrySetResult(); await Release.Task.WaitAsync(token);
            return new ScheduleSnapshotAttempt(fake.BuildSnapshot(student), "");
        }
    }
    private sealed class Invalidated : IScheduleRefresh
    { public Task RefreshInBackgroundAsync(string sessionId, CancellationToken cancellationToken) => throw new ScheduleException(409, "changed"); }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
}
