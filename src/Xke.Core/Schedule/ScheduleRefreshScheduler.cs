using System.Globalization;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;

namespace Xke.Core.Schedule;

public sealed class ScheduleRefreshScheduler
{
    private readonly IScheduleRefresh _refresh;
    private readonly TimeProvider _clock;
    private string? _configuration;
    private ScheduleRefreshState _state = new(false, null, null, false, string.Empty);

    public ScheduleRefreshScheduler(IScheduleRefresh refresh, TimeProvider clock)
    {
        _refresh = refresh;
        _clock = clock;
    }

    public ScheduleRefreshState State => Volatile.Read(ref _state);

    public async Task RunDueAsync(string? sessionId, ScheduleSettingsDocument? settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || settings?.AutoRefreshEnabled != true)
        {
            _configuration = null;
            Volatile.Write(ref _state, new(false, null, null, false, string.Empty));
            return;
        }
        var key = ConfigurationKey(sessionId, settings);
        var now = _clock.GetUtcNow();
        if (_configuration != key)
        {
            _configuration = key;
            Volatile.Write(ref _state, new(true, NextRun(settings), null, false, string.Empty));
        }
        if (State.NextRunAt > now) return;

        var error = string.Empty;
        var requiresLogin = false;
        try { await _refresh.RefreshInBackgroundAsync(sessionId, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ScheduleException ex) when (ex.StatusCode == 409)
        {
            _configuration = null;
            Volatile.Write(ref _state, new(false, null, State.LastRunAt, false, string.Empty));
            return;
        }
        catch (ScheduleException ex) when (ex.StatusCode == 401)
        {
            error = ex.Message;
            requiresLogin = true;
        }
        catch (Exception ex) { error = ex.Message; }
        Volatile.Write(ref _state, new(true, NextRun(settings), _clock.GetUtcNow(), requiresLogin, error));
    }

    internal static string ConfigurationKey(string sessionId, ScheduleSettingsDocument settings)
        => System.Text.Json.JsonSerializer.Serialize(new { sessionId, settings.AutoRefreshEnabled, settings.Source,
            settings.AcademicTerm, settings.AutoRefreshMode, settings.AutoRefreshDailyTime, settings.AutoRefreshIntervalHours });

    private DateTimeOffset NextRun(ScheduleSettingsDocument settings)
    {
        if (settings.AutoRefreshMode == "interval")
        {
            return _clock.GetUtcNow().AddHours(settings.AutoRefreshIntervalHours);
        }
        var localNow = _clock.GetLocalNow();
        var time = TimeOnly.ParseExact(settings.AutoRefreshDailyTime, "HH:mm", CultureInfo.InvariantCulture);
        var target = localNow.Date.Add(time.ToTimeSpan());
        if (target <= localNow.DateTime) target = target.AddDays(1);
        return new DateTimeOffset(target, _clock.LocalTimeZone.GetUtcOffset(target));
    }
}
