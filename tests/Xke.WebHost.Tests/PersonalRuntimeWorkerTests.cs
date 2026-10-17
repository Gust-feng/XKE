using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Desktop;
using Xke.Core.Network;
using Xke.Core.Schedule;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class PersonalRuntimeWorkerTests
{
    [Fact]
    public async Task SlowScheduleRefresh_DoesNotBlockNetworkOrReminders_AndShutdownCancelsAll()
    {
        using var temp = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(temp.RootPath);
        root.Credentials().Save(new StoredCredentialDocument { User = "worker-test", StudentId = "WORKER", Password = "synthetic-only" });
        var student = root.ForStudent("WORKER");
        student.SaveProfile(new StudentProfileDocument { StudentId = "WORKER", Name = "Synthetic Student" });
        student.SavePreferences(new AppSettingsDocument
        {
            Network = new NetworkSettingsDocument { HeartbeatEnabled = true, HeartbeatIntervalMinutes = 1 },
            Schedule = new ScheduleSettingsDocument { AutoRefreshEnabled = true, AutoRefreshMode = "interval", AutoRefreshIntervalHours = 1, LessonReminderEnabled = true, LessonReminderMinutes = 30 },
        });
        student.Schedules().SaveSourceSchedule(new Dictionary<string, object?>
        {
            ["term"] = new Dictionary<string, object?> { ["academic_term"] = "2026-2027-1", ["label"] = "2026-2027-1" },
            ["week_label"] = "第1周",
            ["week_dates"] = new Dictionary<string, object?> { ["星期一"] = "2026-10-12" },
            ["rows"] = new[] { new ScheduleRow { CourseName = "Synthetic Lesson", Weekday = "星期五", Room = "Synthetic Room", TimeSegments = ["09:30-10:15"] } },
        }, new(), "jw_schedule", "第1周");
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = temp.RootPath, WebRootPath = web.RootPath,
            EnableBackgroundTasks = false, ScheduleApiOptions = new ScheduleApiOptions(),
        });
        var clock = new Clock(); var network = new Maintenance(); var schedule = new BlockingRefresh();
        var networkScheduler = new NetworkMaintenanceScheduler(network, clock);
        var scheduleScheduler = new ScheduleRefreshScheduler(schedule, clock);
        var auth = app.Services.GetRequiredService<AuthService>();
        var notifier = new Notifier();
        var reminders = new LessonReminderScheduler(root, clock, notifier, () => auth.ActiveSessionId);
        using var worker = new PersonalRuntimeWorker(auth, root, networkScheduler, scheduleScheduler, NullLogger<PersonalRuntimeWorker>.Instance, reminders);
        await worker.StartAsync(default);
        try
        {
            await network.First.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var init = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (scheduleScheduler.State.NextRunAt is null) await Task.Delay(10, init.Token);
            clock.Advance(TimeSpan.FromHours(1));
            await schedule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(7));
            await notifier.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(7));
            clock.Advance(TimeSpan.FromHours(1));
            await network.Third.Task.WaitAsync(TimeSpan.FromSeconds(7));
            Assert.False(schedule.Exited.Task.IsCompleted);
            Assert.True(network.Calls >= 3);
        }
        finally
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await worker.StopAsync(stop.Token);
        }
        Assert.True(schedule.Exited.Task.IsCompleted);
    }

    private sealed class Clock : TimeProvider
    {
        private long _ticks = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero).Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("worker-test-local", TimeSpan.FromHours(8), "test", "test");
        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private sealed class Notifier : IDesktopNotifier
    {
        public bool IsSupported => true;
        public TaskCompletionSource Delivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken)
        { if (!canDeliver()) return Task.FromResult(false); Delivered.TrySetResult(); return Task.FromResult(true); }
    }

    private sealed class Maintenance : INetworkMaintenance
    {
        public int Calls;
        public TaskCompletionSource First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Third { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<(string Message, NetworkStatusPayload Payload)> MaintainInBackgroundAsync(string id, CancellationToken token = default)
        {
            var count = Interlocked.Increment(ref Calls);
            if (count >= 1) First.TrySetResult();
            if (count >= 3) Third.TrySetResult();
            return Task.FromResult(("ok", new NetworkStatusPayload("", true, true, false, "", [], [], [], new(), "campus", "", "none", [], "")));
        }
        public Task<(string Message, NetworkStatusPayload Payload)> MaintainAsync(string id, NetworkActionRequest request, CancellationToken token = default)
            => MaintainInBackgroundAsync(id, token);
    }

    private sealed class BlockingRefresh : IScheduleRefresh
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task RefreshInBackgroundAsync(string id, CancellationToken token = default)
        {
            Entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { Exited.TrySetResult(); }
        }
    }
}
