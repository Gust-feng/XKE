using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;

namespace Xke.Core.Network;

public sealed class NetworkMaintenanceScheduler
{
    private readonly INetworkMaintenance _maintenance;
    private readonly TimeProvider _clock;
    private string? _configuration;
    private NetworkMaintenanceState _state = new(false, null, null, string.Empty, string.Empty);

    public NetworkMaintenanceScheduler(INetworkMaintenance maintenance, TimeProvider clock)
    {
        _maintenance = maintenance;
        _clock = clock;
    }

    public NetworkMaintenanceState State => Volatile.Read(ref _state);

    public async Task RunDueAsync(string? sessionId, NetworkSettingsDocument? settings, CancellationToken cancellationToken, long credentialRevision = 0)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || settings?.HeartbeatEnabled != true)
        {
            _configuration = null;
            Volatile.Write(ref _state, new(false, null, null, string.Empty, string.Empty));
            return;
        }

        var interval = settings.HeartbeatIntervalMinutes;
        if (interval <= 0) throw new ArgumentOutOfRangeException(nameof(settings));
        var now = _clock.GetUtcNow();
        var configuration = JsonSerializer.Serialize(new { sessionId, settings, credentialRevision });
        if (_configuration != configuration)
        {
            _configuration = configuration;
            Volatile.Write(ref _state, new(true, now, null, string.Empty, string.Empty));
        }
        if (State.NextRunAt > now) return;

        var message = string.Empty;
        var error = string.Empty;
        try
        {
            var result = await _maintenance.MaintainInBackgroundAsync(sessionId, cancellationToken).ConfigureAwait(false);
            message = result.Message;
            error = result.Payload.OperationError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (NetworkException ex) when (ex.StatusCode == 409)
        {
            _configuration = null; // Re-evaluate the newest configuration on the next worker tick.
            message = ex.Detail;
        }
        catch (Exception ex) { error = ex.Message; }

        var completedAt = _clock.GetUtcNow();
        Volatile.Write(ref _state, new(true, completedAt.AddMinutes(interval), completedAt, message, error));
    }
}
