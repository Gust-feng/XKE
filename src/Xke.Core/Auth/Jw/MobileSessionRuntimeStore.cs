using System.Collections.Concurrent;

namespace Xke.Core.Auth.Jw;

public sealed class MobileSessionRuntimeStore
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.Ordinal);

    // Mobile JW tokens stay process-local on purpose so host restarts fall back
    // to local session restore with connected=false instead of silently reusing
    // remote state across boots.
    public void StoreToken(string sessionId, string token)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        var cleanedToken = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId) || string.IsNullOrWhiteSpace(cleanedToken))
        {
            return;
        }

        _tokens[cleanedSessionId] = cleanedToken;
    }

    public bool TryGetToken(string sessionId, out string token)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            token = string.Empty;
            return false;
        }

        if (_tokens.TryGetValue(cleanedSessionId, out var stored))
        {
            token = stored;
            return true;
        }

        token = string.Empty;
        return false;
    }

    public void Remove(string sessionId)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            return;
        }

        _tokens.TryRemove(cleanedSessionId, out _);
    }
}
