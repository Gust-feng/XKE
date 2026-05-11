using System.Security.Cryptography;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeCredentialStore
{
    private readonly XkeDataRoot _root;
    private readonly ProtectedJsonFileStore _protectedJson = new();

    public XkeCredentialStore(XkeDataRoot root)
    {
        _root = root;
    }

    public StoredCredentialDocument? Load(string user)
    {
        return _protectedJson.Read<StoredCredentialDocument>(GetPath(user));
    }

    public string Save(StoredCredentialDocument document)
    {
        var path = GetPath(document.User);
        _protectedJson.Write(path, document);
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
                var document = _protectedJson.Read<StoredCredentialDocument>(path);
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
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string GetPath(string user)
    {
        return Path.Combine(_root.CredentialsDirectory, $"{XkeDataRoot.SanitizePathSegment(user)}.json.dpapi");
    }
}
