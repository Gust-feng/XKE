using Xke.Contracts.Responses;

namespace Xke.Core.Telemetry;

public sealed class LocalOnlyTelemetryTransport : ITelemetryTransport
{
    public Task<TelemetryCloudResult> ReportAsync(
        Dictionary<string, object?> payload,
        string skipReason,
        CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(skipReason) ? "spooled-local-only" : skipReason.Trim();
        return Task.FromResult(new TelemetryCloudResult(
            Ok: true,
            Queued: true,
            Skipped: false,
            Reason: reason));
    }
}
