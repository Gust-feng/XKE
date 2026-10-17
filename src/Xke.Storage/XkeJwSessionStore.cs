using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeJwSessionStore
{
    private readonly XkeDataRoot _root;
    private readonly ProtectedJsonFileStore _protectedJson = new();

    public XkeJwSessionStore(XkeDataRoot root)
    {
        _root = root;
    }

    public JwSessionDocument? Load(string user)
    {
        return _protectedJson.Read<JwSessionDocument>(GetPath(user));
    }

    public void Save(JwSessionDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.User) || document.Cookies.Count == 0)
        {
            return;
        }

        _protectedJson.Write(GetPath(document.User), document);
    }

    public void Remove(string user)
    {
        var path = GetPath(user);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetPath(string user)
    {
        return Path.Combine(_root.JwSessionsDirectory, $"{XkeDataRoot.SanitizePathSegment(user)}.json.dpapi");
    }
}
