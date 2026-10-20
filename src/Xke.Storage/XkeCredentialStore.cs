using System.Security.Cryptography;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeCredentialStore
{
    private static readonly object[] FileGates = Enumerable.Range(0, 32).Select(_ => new object()).ToArray();
    private readonly XkeDataRoot _root;
    private readonly ProtectedJsonFileStore _protectedJson = new();

    public XkeCredentialStore(XkeDataRoot root)
    {
        _root = root;
    }

    public StoredCredentialDocument? Load(string user)
    {
        return ReadPath(GetPath(user));
    }

    public string Save(StoredCredentialDocument document)
    {
        var path = GetPath(document.User);
        lock (GetFileGate(path)) AtomicFile.Write(path, temporary => _protectedJson.Write(temporary, document));
        return _root.RelativePath(path);
    }

    public bool Exists(string user)
    {
        return File.Exists(GetPath(user));
    }

    public IReadOnlyList<StoredCredentialDocument> LoadAll()
    {
        if (!Directory.Exists(_root.CredentialsDirectory))
        {
            return Array.Empty<StoredCredentialDocument>();
        }

        var documents = new List<StoredCredentialDocument>();
        foreach (var path in Directory.EnumerateFiles(_root.CredentialsDirectory, "*.json.dpapi", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var document = ReadPath(path);
                if (document is not null && !string.IsNullOrWhiteSpace(document.User))
                {
                    documents.Add(document);
                }
            }
            catch (CryptographicException)
            {
            }
            catch (FormatException)
            {
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return documents;
    }

    public void Remove(string user)
    {
        var path = GetPath(user);
        lock (GetFileGate(path))
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private StoredCredentialDocument? ReadPath(string path)
    {
        lock (GetFileGate(path)) return _protectedJson.Read<StoredCredentialDocument>(path);
    }

    // Status reads and authentication commits create separate store instances.
    // Use the existing fixed-stripe pattern rather than per-instance locks.
    private static object GetFileGate(string path)
        => FileGates[(int)((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(path) % (uint)FileGates.Length)];

    public string GetPath(string user)
    {
        return Path.Combine(_root.CredentialsDirectory, $"{XkeDataRoot.SanitizePathSegment(user)}.json.dpapi");
    }
}
