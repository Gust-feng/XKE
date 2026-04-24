using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeSessionStore
{
    private readonly XkeDataRoot _root;
    private readonly ProtectedJsonFileStore _protectedJson = new();

    public XkeSessionStore(XkeDataRoot root)
    {
        _root = root;
    }

    public (string SessionId, StoredSessionDocument Session) Create(string user, string studentId, string preloginMode)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.Now.ToString("O");
        var document = new StoredSessionDocument
        {
            SessionIdHash = XkeDataRoot.HashSessionId(sessionId),
            User = user.Trim(),
            StudentId = studentId.Trim(),
            PreloginMode = preloginMode.Trim(),
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };

        Save(sessionId, document);
        return (sessionId, document);
    }

    public StoredSessionDocument? Load(string sessionId)
    {
        return _protectedJson.Read<StoredSessionDocument>(GetPath(sessionId));
    }

    public string Save(string sessionId, StoredSessionDocument session)
    {
        var path = GetPath(sessionId);
        _protectedJson.Write(path, session);
        return _root.RelativePath(path);
    }

    public void Remove(string sessionId)
    {
        var path = GetPath(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public int Count()
    {
        return Directory.Exists(_root.SessionsDirectory)
            ? Directory.EnumerateFiles(_root.SessionsDirectory, "*.json.dpapi", SearchOption.TopDirectoryOnly).Count()
            : 0;
    }

    public string GetPath(string sessionId)
    {
        return Path.Combine(_root.SessionsDirectory, $"{XkeDataRoot.HashSessionId(sessionId)}.json.dpapi");
    }
}
