using Xke.Contracts.Requests;
using Xke.Contracts.Responses;

namespace Xke.Core.Network;

public interface INetworkMaintenance
{
    Task<(string Message, NetworkStatusPayload Payload)> MaintainInBackgroundAsync(
        string sessionId, CancellationToken cancellationToken = default);

    Task<(string Message, NetworkStatusPayload Payload)> MaintainAsync(
        string sessionId, NetworkActionRequest request, CancellationToken cancellationToken = default);
}
