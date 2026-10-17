using System.Collections.Concurrent;

namespace Xke.Core.Schedule;

public sealed class TraditionalJwSessionRuntimeStore : IDisposable
{
    private readonly ConcurrentDictionary<string, TraditionalJwClient> _clients = new(StringComparer.Ordinal);

    // Active HTTP clients are process-local. The desktop credential store can
    // separately restore a protected cookie document after a successful GET.
    public void Store(string sessionId, TraditionalJwClient client)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            return;
        }

        if (_clients.TryGetValue(cleanedSessionId, out var existing) && !ReferenceEquals(existing, client))
        {
            existing.Dispose();
        }

        _clients[cleanedSessionId] = client;
    }

    public bool TryGet(string sessionId, out TraditionalJwClient client)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            client = null!;
            return false;
        }

        return _clients.TryGetValue(cleanedSessionId, out client!);
    }

    public void Remove(string sessionId)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            return;
        }

        if (_clients.TryRemove(cleanedSessionId, out var client))
        {
            client.Dispose();
        }
    }
    public void Dispose()
    {
        foreach (var sessionId in _clients.Keys) Remove(sessionId);
    }

}
