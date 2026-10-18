using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xke.Core.Auth;
using Xke.Core.Network;
using Xke.Core.Schedule;
using Xke.Storage;

namespace Xke.WebHost;

internal sealed class PersonalRuntimeWorker : BackgroundService
{
    private readonly AuthService _auth;
    private readonly XkeDataRoot _data;
    private readonly NetworkMaintenanceScheduler _scheduler;
    private readonly ScheduleRefreshScheduler _schedule;
    private readonly ILogger<PersonalRuntimeWorker> _logger;
    private readonly LessonReminderScheduler? _reminders;

    public PersonalRuntimeWorker(AuthService auth, XkeDataRoot data, NetworkMaintenanceScheduler scheduler, ScheduleRefreshScheduler schedule, ILogger<PersonalRuntimeWorker> logger, LessonReminderScheduler? reminders = null)
    {
        _auth = auth;
        _data = data;
        _scheduler = scheduler;
        _schedule = schedule;
        _logger = logger;
        _reminders = reminders;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        // Restore local trust without starting a remote login or loading WebView.
        try { _auth.DesktopBootstrap(); }
        catch (Exception ex)
        {
            _logger.LogInformation("本地工作区未恢复，后台任务等待登录：{Reason}", ex.Message);
        }

        await Task.WhenAll(
            RunPeriodicallyAsync(RunNetworkAsync, "校园网保活", stoppingToken),
            RunPeriodicallyAsync(RunScheduleAsync, "课表刷新", stoppingToken),
            RunPeriodicallyAsync(RunRemindersAsync, "课前提醒", stoppingToken)).ConfigureAwait(false);
    }

    private async Task RunNetworkAsync(CancellationToken token)
    {
        var (sessionId, student) = ActiveStudent();
        var settings = student?.LoadPreferences()?.Network;
        var revision = settings?.HeartbeatEnabled == true ? student!.NetworkCredentials().GetRevision(settings.Provider) : string.Empty;
        await _scheduler.RunDueAsync(sessionId, settings, token, revision).ConfigureAwait(false);
    }

    private Task RunScheduleAsync(CancellationToken token)
    {
        var (sessionId, student) = ActiveStudent();
        return _schedule.RunDueAsync(sessionId, student?.LoadPreferences()?.Schedule, token);
    }

    private Task RunRemindersAsync(CancellationToken token)
        => _reminders?.RunDueAsync(_auth.ActiveSessionId, token) ?? Task.CompletedTask;

    private (string? SessionId, XkeStudentStore? Student) ActiveStudent()
    {
        var id = _auth.ActiveSessionId;
        var session = id is null ? null : _data.Sessions().Load(id);
        return session is null ? (null, null) : (id, _data.ForStudent(session.StudentId));
    }

    private async Task RunPeriodicallyAsync(Func<CancellationToken, Task> action, string name, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            do
            {
                try { await action(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logger.LogWarning(ex, "{TaskName}本轮执行失败。", name); }
            }
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
