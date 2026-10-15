using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Desktop;
using Xke.Core.Schedule;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class LessonReminderTests
{
    [Fact]
    public void ReminderSettings_DefaultThirtyAndNeverBelowFifteen()
    {
        Assert.Equal(30, AuthSettingsCatalog.CreateDefault().Schedule.LessonReminderMinutes);
        Assert.Equal(30, new ScheduleSettingsDocument().LessonReminderMinutes);
        foreach (var minutes in new[] { 1, 5, 10, 15, 18, 30, 45 })
        {
            var settings = AuthSettingsCatalog.Apply(AuthSettingsCatalog.CreateDefault(), new() { Schedule = new() { LessonReminderEnabled = true, LessonReminderMinutes = minutes } });
            Assert.Equal(Math.Max(15, minutes), settings.Schedule.LessonReminderMinutes);
            Assert.Equal(settings.Schedule.LessonReminderMinutes, AuthSettingsCatalog.Normalize(settings).Schedule.LessonReminderMinutes);
        }
    }

    [Fact]
    public async Task SendsWithBuffer_AndDeduplicatesAcrossTicksAndRestart()
    {
        using var f = new Fixture();
        await f.Run();
        Assert.Empty(f.Notifier.Messages);
        f.Clock.Local = new(2026, 10, 16, 7, 30, 0);
        await f.Run(); await f.Run();
        await f.NewScheduler().RunDueAsync(f.Session, default);
        var message = Assert.Single(f.Notifier.Messages);
        Assert.Contains("30 分钟", message); Assert.Contains("08:00", message); Assert.Contains("测试教室", message);
        var receipt = File.ReadAllText(Path.Combine(f.Student.InfoDirectory, "lesson_reminders.json"));
        Assert.DoesNotContain("合成课程", receipt); Assert.DoesNotContain("测试教室", receipt);
    }

    [Fact]
    public async Task NoCatchUpAfterSleepOrCourseStart()
    {
        using var f = new Fixture();
        f.Clock.Local = new(2026, 10, 16, 7, 35, 0); await f.Run();
        f.Clock.Local = new(2026, 10, 16, 8, 0, 0); await f.Run();
        Assert.Empty(f.Notifier.Messages);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("logout")]
    [InlineData("switched")]
    [InlineData("unsupported")]
    [InlineData("no-calendar")]
    public async Task DoesNotNotifyWithoutValidContext(string change)
    {
        using var f = new Fixture(); f.Clock.Local = new(2026, 10, 16, 7, 30, 0);
        if (change == "disabled") f.Configure(false);
        if (change == "logout") f.Root.Sessions().Remove(f.Session);
        if (change == "switched") f.Active = "other-session";
        if (change == "unsupported") f.Notifier.Supported = false;
        if (change == "no-calendar") f.SaveWeek([Row()], "2026-10-16T00:00:00Z", dates: false);
        await f.Run(); Assert.Empty(f.Notifier.Messages);
    }

    [Fact]
    public async Task NewerEmptyWeekOverridesSemester_AndHistoricalBrowsingDoesNotChangeReminderTerm()
    {
        using var f = new Fixture(); f.Clock.Local = new(2026, 10, 16, 7, 30, 0);
        f.Student.Schedules().SaveSemesterSchedule(Semester([Row()], "2026-10-14T00:00:00Z"));
        f.SaveWeek([], "2026-10-16T00:00:00Z");
        await f.Run(); Assert.Empty(f.Notifier.Messages);
        f.SaveWeek([Row()], "2026-10-17T00:00:00Z");
        f.SaveWeek([], "2026-10-18T00:00:00Z", term: "2025-2026-2");
        await f.Run(); Assert.Single(f.Notifier.Messages);
    }

    [Fact]
    public async Task NewerSemesterOverridesOldWeek_AndOnlyUsesMatchingWeekRules()
    {
        using var f = new Fixture(); f.Clock.Local = new(2026, 10, 16, 7, 30, 0);
        f.Student.Schedules().SaveSemesterSchedule(Semester([Row(2)], "2026-10-17T00:00:00Z"));
        await f.Run(); Assert.Empty(f.Notifier.Messages);
    }

    [Fact]
    public async Task ThirtyMinuteBufferCanCrossMidnight()
    {
        using var f = new Fixture();
        f.SaveWeek([new ScheduleRow { CourseName = "午夜合成课", Weekday = "星期六", Room = "测试教室", TimeSegments = ["00:10-00:50"], WeekNumbers = [1] }], "2026-10-16T00:00:00Z");
        f.Clock.Local = new(2026, 10, 16, 23, 40, 0);
        await f.Run(); Assert.Single(f.Notifier.Messages);
    }

    [Fact]
    public async Task FailedNotificationIsNotRecorded_AndCancellationStopsDelivery()
    {
        using var f = new Fixture(); f.Clock.Local = new(2026, 10, 16, 7, 30, 0);
        f.Notifier.Accept = false; await f.Run();
        Assert.False(File.Exists(Path.Combine(f.Student.InfoDirectory, "lesson_reminders.json")));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Scheduler.RunDueAsync(f.Session, cancel.Token));
        f.Notifier.Accept = true; await f.Run();
        Assert.Single(f.Notifier.Messages);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("disable")]
    [InlineData("late")]
    public async Task QueuedNativeNotification_RevalidatesBeforeActualDelivery(string change)
    {
        using var f = new Fixture(); f.Clock.Local = new(2026, 10, 16, 7, 30, 0);
        f.Notifier.BeforeDeliver = () =>
        {
            if (change == "logout") f.Root.Sessions().Remove(f.Session);
            if (change == "disable") f.Configure(false);
            if (change == "late") f.Clock.Local = new(2026, 10, 16, 7, 40, 0);
        };
        await f.Run(); Assert.Empty(f.Notifier.Messages);
        Assert.False(File.Exists(Path.Combine(f.Student.InfoDirectory, "lesson_reminders.json")));
    }

    [Fact]
    public void Planner_MergesShortBreaksButKeepsRoomChanges_AndSkipsUnscheduledRows()
    {
        ScheduleRow Make(string time, string room = "A") => new() { CourseName = "合成课", Teacher = "T", Room = room, Weekday = "星期五", TimeSegments = [time], WeekNumbers = [1] };
        var rows = new[] { Make("08:00-08:45"), Make("08:55-09:40"), Make("09:50-10:35", "B"), Make(""), Make("22:00-21:00") };
        var lessons = LessonReminderPlanner.ForDay(rows, new(2026, 10, 16), 1, false);
        Assert.Equal(2, lessons.Count); Assert.Equal(new TimeOnly(8, 0), lessons[0].Start); Assert.Equal(new TimeOnly(9, 40), lessons[0].End);
        Assert.Empty(LessonReminderPlanner.ForDay(rows, new(2026, 10, 16), 2, false));
        Assert.Equal(2, LessonReminderPlanner.ForDay(rows, new(2026, 10, 16), 2, true).Count);
    }

    private static ScheduleRow Row(int week = 1) => new() { CourseName = "合成课程", Teacher = "合成教师", Room = "测试教室", Weekday = "星期五", TimeSegments = ["08:00-08:45", "08:55-09:40"], WeekNumbers = [week] };
    private static SemesterScheduleData Semester(IReadOnlyList<ScheduleRow> rows, string at)
        => new(new("合成学期", "2026-2027-1"), new("REMINDER", "合成同学"), rows, [], [], [], [], at, []);

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory _temp = new();
        public XkeDataRoot Root { get; }
        public XkeStudentStore Student { get; }
        public string Session { get; }
        public string? Active { get; set; }
        public Clock Clock { get; } = new();
        public Notifier Notifier { get; } = new();
        public LessonReminderScheduler Scheduler { get; }
        public Fixture()
        {
            Root = new(_temp.RootPath); Student = Root.ForStudent("REMINDER");
            Session = Root.Sessions().Create("synthetic", Student.StudentId, "auto").SessionId; Active = Session;
            Configure(true); SaveWeek([Row()], "2026-10-15T00:00:00Z"); Scheduler = NewScheduler();
        }
        public void Configure(bool enabled) => Student.SavePreferences(new() { Schedule = new() { LessonReminderEnabled = enabled, LessonReminderMinutes = 30 } });
        public void SaveWeek(ScheduleRow[] rows, string at, bool dates = true, string term = "2026-2027-1")
        {
            var payload = new Dictionary<string, object?> { ["term"] = new Dictionary<string, object?> { ["academic_term"] = term }, ["rows"] = rows, ["week_label"] = "第1周", ["updated_at"] = at };
            payload["week_dates"] = dates ? new Dictionary<string, object?> { ["星期一"] = "2026-10-12", ["星期五"] = "2026-10-16" } : new();
            Student.Schedules().SaveSourceSchedule(payload, new(), "jw_schedule", "第1周");
        }
        public LessonReminderScheduler NewScheduler() => new(Root, Clock, Notifier, () => Active);
        public Task Run() => Scheduler.RunDueAsync(Session, default);
        public void Dispose() => _temp.Dispose();
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Local { get; set; } = new(2026, 10, 16, 7, 29, 0);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("reminder-test", TimeSpan.FromHours(8), "test", "test");
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(Local, TimeSpan.FromHours(8)).ToUniversalTime();
    }
    private sealed class Notifier : IDesktopNotifier
    {
        public bool Supported = true; public bool Accept = true; public Action? BeforeDeliver;
        public bool IsSupported => Supported;
        public List<string> Messages { get; } = [];
        public Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); BeforeDeliver?.Invoke(); var accepted = Accept && canDeliver(); if (accepted) Messages.Add(message); return Task.FromResult(accepted); }
    }
}
