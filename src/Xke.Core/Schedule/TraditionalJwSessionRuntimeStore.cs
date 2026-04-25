using System.Collections.Concurrent;

namespace Xke.Core.Schedule;

public sealed class TraditionalJwSessionRuntimeStore
{
    private readonly ConcurrentDictionary<string, TraditionalJwClient> _clients = new(StringComparer.Ordinal);

    // Traditional JW cookies and ticket-derived sessions are deliberately kept
    // process-local. A host restart must require a new JW bootstrap instead of
    // silently reviving browser-like Cookie state from disk.
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
}
