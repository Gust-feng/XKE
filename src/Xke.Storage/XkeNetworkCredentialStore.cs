using System.Security.Cryptography;
using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeNetworkCredentialStore
{
    private static readonly object[] FileGates = Enumerable.Range(0, 32).Select(_ => new object()).ToArray();
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
        var path = GetPath(provider);
        lock (GetFileGate(path)) return _protectedJson.Read<StoredNetworkCredentialDocument>(path);
    }

    // Hash ciphertext, not timestamps or plaintext. Rapid replacements may share a timestamp.
    public string GetRevision(string provider)
    {
        var path = GetPath(provider);
        lock (GetFileGate(path))
        {
            try
            {
                using var file = File.OpenRead(path);
                return Convert.ToHexString(SHA256.HashData(file));
            }
            catch (FileNotFoundException) { return string.Empty; }
            catch (DirectoryNotFoundException) { return string.Empty; }
        }
    }

    public string Save(StoredNetworkCredentialDocument document)
    {
        var path = GetPath(document.Provider);
        lock (GetFileGate(path)) AtomicFile.Write(path, temporary => _protectedJson.Write(temporary, document));
        return _root.RelativePath(path);
    }

    public void Remove(string provider)
    {
        var path = GetPath(provider);
        lock (GetFileGate(path))
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public string GetPath(string provider)
    {
        return Path.Combine(
            _studentStore.NetworkInfoDirectory,
            $"{NormalizeProvider(provider)}.json.dpapi");
    }

    // Student-store helpers are short-lived; independent instances must share the same file gate.
    private static object GetFileGate(string path)
        => FileGates[(int)((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(path) % (uint)FileGates.Length)];

    private static string NormalizeProvider(string provider)
    {
        var cleaned = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "campus" : XkeDataRoot.SanitizePathSegment(cleaned);
    }
}
