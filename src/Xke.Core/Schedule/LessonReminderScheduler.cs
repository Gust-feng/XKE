using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Desktop;
using Xke.Storage;

namespace Xke.Core.Schedule;

/// <summary>Local-only reminders, independent of WebView and remote schedule refresh.</summary>
public sealed class LessonReminderScheduler
{
    private readonly XkeDataRoot _data;
    private readonly TimeProvider _clock;
    private readonly IDesktopNotifier _notifier;
    private readonly Func<string?> _activeSession;
    private readonly HashSet<string> _delivered = [];
    private DateOnly _receiptDay;

    public LessonReminderScheduler(XkeDataRoot data, TimeProvider clock, IDesktopNotifier notifier, Func<string?> activeSession)
        => (_data, _clock, _notifier, _activeSession) = (data, clock, notifier, activeSession);

    public async Task RunDueAsync(string? sessionId, CancellationToken cancellationToken)
    {
        if (!_notifier.IsSupported || sessionId is null || _activeSession() != sessionId) return;
        var session = _data.Sessions().Load(sessionId);
        if (session is null) return;
        var student = _data.ForStudent(string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId);
        var settings = AuthSettingsCatalog.Normalize(student.LoadPreferences()).Schedule;
        if (!settings.LessonReminderEnabled) return;
        var localNow = _clock.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(localNow);
        if (_receiptDay != today) { _delivered.Clear(); _receiptDay = today; }
        var schedules = student.Schedules();
        var sources = XkeScheduleStore.ResolveSourceKeys(settings.Source);
        var semester = schedules.LoadSemesterSchedule();
        var latest = schedules.LoadLatestTermSourceSchedule(sources);
        var weekTerm = (latest?.GetValueOrDefault("term") as Dictionary<string, object?>)?.GetValueOrDefault("academic_term")?.ToString() ?? "";
        var term = string.CompareOrdinal(semester?.Term.AcademicTerm ?? "", weekTerm) >= 0 ? semester?.Term.AcademicTerm ?? "" : weekTerm;
        if (term.Length == 0 || schedules.FindSemesterStartDate(term, sources) is not { } start) return;
        if (semester?.Term.AcademicTerm != term) semester = null;
        var receipts = new XkeLessonReminderStore(student);
        // A buffer can cross midnight. Never infer tomorrow from a partial week with no matching cache.
        foreach (var date in new[] { today, today.AddDays(1) })
        {
            var week = (date.DayNumber - start.DayNumber) / 7 + 1;
            if (date < start || week < 1) continue;
            var snapshot = schedules.LoadScheduleForWeek($"第{week}周", sources, term);
            var useWeek = snapshot is not null && (semester is null || Timestamp(snapshot.GetValueOrDefault("updated_at")?.ToString()) >= Timestamp(semester.UpdatedAt));
            var rows = useWeek ? JsonSerializer.SerializeToElement(snapshot!.GetValueOrDefault("rows")).Deserialize<ScheduleRow[]>() ?? [] : semester?.Rows ?? [];
            foreach (var lesson in LessonReminderPlanner.ForDay(rows, date, week, useWeek))
            {
                var startsAt = date.ToDateTime(lesson.Start);
                var dueAt = startsAt.AddMinutes(-settings.LessonReminderMinutes);
                // No stale catch-up after sleep or restart; a small delivery window tolerates a timer tick.
                if (localNow < dueAt || localNow - dueAt > TimeSpan.FromMinutes(2) || localNow >= startsAt) continue;
                var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{term}|{date:yyyy-MM-dd}|{lesson.Start:HH:mm}|{lesson.Name}|{lesson.Teacher}|{lesson.Room}")));
                var key = $"{student.StudentId}|{date}|{fingerprint}";
                if (_delivered.Contains(key)) continue;
                if (receipts.Contains(date, fingerprint)) { _delivered.Add(key); continue; }
                cancellationToken.ThrowIfCancellationRequested();
                bool CanDeliver()
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var current = AuthSettingsCatalog.Normalize(student.LoadPreferences()).Schedule;
                    var deliveryTime = _clock.GetLocalNow().DateTime;
                    return _activeSession() == sessionId && _data.Sessions().Load(sessionId) is not null
                        && current.LessonReminderEnabled && current.LessonReminderMinutes == settings.LessonReminderMinutes
                        && current.Source == settings.Source && deliveryTime >= dueAt
                        && deliveryTime - dueAt <= TimeSpan.FromMinutes(2) && deliveryTime < startsAt;
                }
                if (!CanDeliver()) return;
                var minutes = (int)Math.Ceiling((startsAt - localNow).TotalMinutes);
                var message = $"{lesson.Name} · {lesson.Start:HH:mm} 开始（约 {minutes} 分钟后）";
                if (!string.IsNullOrWhiteSpace(lesson.Room)) message += $"\n教室：{lesson.Room}";
                if (!await _notifier.ShowAsync("课前提醒", message, CanDeliver, cancellationToken).ConfigureAwait(false)) continue;
                _delivered.Add(key);
                receipts.Record(date, fingerprint, today);
            }
        }
    }

    private static DateTimeOffset Timestamp(string? value) => DateTimeOffset.TryParse(value, out var time) ? time : DateTimeOffset.MinValue;
}
