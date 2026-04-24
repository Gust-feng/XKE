using Xke.Contracts.Responses;

namespace Xke.Core.Telemetry;

public interface ITelemetryTransport
{
    Task<TelemetryCloudResult> ReportAsync(
        Dictionary<string, object?> payload,
        string skipReason,
        CancellationToken cancellationToken = default);
}
