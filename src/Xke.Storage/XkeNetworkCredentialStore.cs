using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeNetworkCredentialStore
{
    private readonly XkeDataRoot _root;
    private readonly XkeStudentStore _studentStore;
    private readonly ProtectedJsonFileStore _protectedJson = new();

    public XkeNetworkCredentialStore(XkeDataRoot root, XkeStudentStore studentStore)
    {
        _root = root;
        _studentStore = studentStore;
    }

    public StoredNetworkCredentialDocument? Load(string provider)
    {
        return _protectedJson.Read<StoredNetworkCredentialDocument>(GetPath(provider));
    }

    // An opaque change marker; scheduling must not decrypt passwords on every tick.
    public long GetRevision(string provider)
    {
        var path = GetPath(provider);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
    }

    public string Save(StoredNetworkCredentialDocument document)
    {
        var path = GetPath(document.Provider);
        _protectedJson.Write(path, document);
        return _root.RelativePath(path);
    }

    public void Remove(string provider)
    {
        var path = GetPath(provider);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string GetPath(string provider)
    {
        return Path.Combine(
            _studentStore.NetworkInfoDirectory,
            $"{NormalizeProvider(provider)}.json.dpapi");
    }

    private static string NormalizeProvider(string provider)
    {
        var cleaned = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "campus" : XkeDataRoot.SanitizePathSegment(cleaned);
    }
}
