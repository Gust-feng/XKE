using Xke.Contracts.Responses;

namespace Xke.Core.Releases;

/// <summary>Serial update state machine, independent of school credentials and account sessions.</summary>
public sealed class AppUpdateService : IDisposable
{
    private readonly IAppUpdateBackend _backend;
    private readonly TimeProvider _clock;
    private readonly Action? _requestRestart;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppUpdateState _state;
    private DateTimeOffset _nextAutomaticCheck;
    private bool _restartRequested;
    private CancellationTokenSource? _automaticWork;
    private readonly object _automaticSync = new();
    private readonly object _manualSync = new();
    private CancellationTokenSource _lifetime = new();
    private Task<AppUpdateState>? _manualDownload;

    public AppUpdateService(IAppUpdateBackend backend, TimeProvider clock, Action? requestRestart = null)
    {
        _backend = backend; _clock = clock; _requestRestart = requestRestart;
        _state = new(backend.CurrentVersion ?? AppRelease.Version, AppRelease.RepositoryUrl, AppRelease.ReleasesUrl,
            backend.PendingVersion, !backend.IsInstalled ? "unsupported" : backend.PendingVersion is null ? "idle" : "ready", backend.PendingVersion is null ? 0 : 100, "", null);
        _nextAutomaticCheck = clock.GetUtcNow().AddSeconds(30);
    }

    public AppUpdateState State => Volatile.Read(ref _state);
    public bool IsInstalled => _backend.IsInstalled;

    public async Task<AppUpdateState> CheckAsync(CancellationToken cancellationToken)
    {
        if (!IsInstalled || State.Status is "ready" or "installing" || !await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return State;
        var previous = State;
        try
        {
            Publish(previous with { Status = "checking", Error = "" });
            var version = await _backend.CheckAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Publish(State with { Status = version is null ? "up_to_date" : "available", LatestVersion = version,
                Progress = 0, Error = "", CheckedAt = _clock.GetUtcNow().ToString("O") });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Publish(previous); throw; }
        catch { Publish(State with { Status = "error", Error = "暂时无法检查更新，请稍后重试" }); }
        finally { _gate.Release(); }
        return State;
    }

    public async Task<AppUpdateState> DownloadAsync(CancellationToken cancellationToken)
    {
        if (!IsInstalled || State.Status is "ready" or "installing" || !await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return State;
        var previous = State;
        try
        {
            if (State.LatestVersion is null) return State;
            Publish(State with { Status = "downloading", Progress = 0, Error = "" });
            await _backend.DownloadAsync(value => Publish(State with { Progress = Math.Clamp(value, 0, 100) }), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (_backend.PendingVersion != State.LatestVersion) throw new InvalidOperationException("更新包尚未就绪");
            Publish(State with { Status = "ready", Progress = 100, Error = "" });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Publish(previous); throw; }
        catch { Publish(State with { Status = "error", Error = "更新下载或校验失败，当前版本仍可使用" }); }
        finally { _gate.Release(); }
        return State;
    }

    public AppUpdateState RequestDownload()
    {
        lock (_manualSync)
        {
            if (_manualDownload?.IsCompleted == false || State.Status is not ("available" or "error") || State.LatestVersion is null) return State;
            // Own the download beyond the HTTP request/panel lifetime; the hosted worker drains it on real exit.
            _manualDownload = DownloadAsync(_lifetime.Token);
            return State;
        }
    }

    public void Resume()
    {
        lock (_manualSync)
        {
            if (!_lifetime.IsCancellationRequested) return;
            _lifetime.Dispose();
            _lifetime = new();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task<AppUpdateState>? pending;
        lock (_manualSync) { _lifetime.Cancel(); pending = _manualDownload; }
        if (pending is null) return;
        try { await pending.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
    }

    public AppUpdateState RequestApply()
    {
        if (State.Status != "ready" || _backend.PendingVersion is null || _requestRestart is null) return State;
        _restartRequested = true;
        Publish(State with { Status = "installing", Error = "" });
        try { _requestRestart(); }
        catch { _restartRequested = false; Publish(State with { Status = "error", Error = "暂时无法重启，请退出后重新打开软件" }); }
        return State;
    }

    /// <summary>Call only after the local host has drained, immediately before a genuine process exit.</summary>
    public bool PrepareExit(bool automaticEnabled, bool restart = false)
    {
        if (!IsInstalled || _backend.PendingVersion is null || !automaticEnabled && !_restartRequested) return false;
        try { _backend.ApplyOnExit(restart || _restartRequested); return true; }
        catch
        {
            Publish(State with { Status = "error", Error = "暂时无法安装更新，当前版本未被替换" });
            if (_restartRequested) throw;
            return false;
        }
    }

    public void SetAutomaticEnabled(bool enabled)
    {
        if (!enabled) lock (_automaticSync) _automaticWork?.Cancel();
    }

    public async Task RunAutomaticAsync(Func<bool> enabled, CancellationToken cancellationToken)
    {
        if (!IsInstalled || !enabled() || State.Status is "ready" or "installing") return;
        if (State.Status != "available" && _clock.GetUtcNow() < _nextAutomaticCheck) return;
        using var automatic = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_automaticSync) _automaticWork = automatic;
        try
        {
            if (State.Status != "available")
            {
                _nextAutomaticCheck = _clock.GetUtcNow().AddHours(6);
                await CheckAsync(automatic.Token).ConfigureAwait(false);
            }
            if (enabled() && State.Status == "available") await DownloadAsync(automatic.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && automatic.IsCancellationRequested) { }
        finally { lock (_automaticSync) { if (ReferenceEquals(_automaticWork, automatic)) _automaticWork = null; } }
    }

    private void Publish(AppUpdateState value) => Volatile.Write(ref _state, value);
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _lifetime.Dispose();
        _gate.Dispose();
    }
}
