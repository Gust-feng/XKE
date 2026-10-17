using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeStudentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;
    private readonly LocalEncryptedFileStore _encrypted = new();

    public XkeStudentStore(XkeDataRoot root, string studentId)
    {
        _root = root;
        StudentId = XkeDataRoot.SanitizePathSegment(studentId);
    }

    public string StudentId { get; }
    public string StudentDirectory => Path.Combine(_root.StudentsDirectory, StudentId);
    public string ProfilePath => Path.Combine(StudentDirectory, "profile.json");
    public string PreferencesPath => Path.Combine(StudentDirectory, "preferences.json");
    public string AvatarBasePath => Path.Combine(StudentDirectory, "avatar");
    public string SchedulesDirectory => Path.Combine(StudentDirectory, "schedules");
    public string InfoDirectory => Path.Combine(StudentDirectory, "info");
    public string NetworkInfoDirectory => Path.Combine(InfoDirectory, "network");
    public string NetworkAuthHistoryPath => Path.Combine(InfoDirectory, "network_auth_history.json");

    public StudentProfileDocument? LoadProfile()
    {
        return ReadBusinessJson<StudentProfileDocument>(ProfilePath);
    }

    public string SaveProfile(StudentProfileDocument profile)
    {
        EnsureDirectory();
        AtomicFile.WriteText(ProfilePath, JsonSerializer.Serialize(profile, JsonOptions));
        return _root.RelativePath(ProfilePath);
    }

    public AppSettingsDocument? LoadPreferences()
    {
        return ReadBusinessJson<AppSettingsDocument>(PreferencesPath);
    }

    public string SavePreferences(AppSettingsDocument settings)
    {
        EnsureDirectory();
        AtomicFile.WriteText(PreferencesPath, JsonSerializer.Serialize(settings, JsonOptions));
        return _root.RelativePath(PreferencesPath);
    }

    public string? GetAvatarPath()
    {
        foreach (var suffix in new[] { ".png", ".jpg", ".jpeg", ".webp" })
        {
            var candidate = AvatarBasePath + suffix;
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public XkeScheduleStore Schedules()
    {
        return new XkeScheduleStore(_root, this);
    }

    public XkeNetworkCredentialStore NetworkCredentials()
    {
        return new XkeNetworkCredentialStore(_root, this);
    }

    public string SaveAvatar(string fileName, byte[] content)
    {
        EnsureDirectory();
        var suffix = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (suffix is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
        {
            suffix = ".png";
        }

        foreach (var existing in new[] { ".png", ".jpg", ".jpeg", ".webp" })
        {
            var path = AvatarBasePath + existing;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        var avatarPath = AvatarBasePath + suffix;
        File.WriteAllBytes(avatarPath, content);
        return _root.RelativePath(avatarPath);
    }

    public byte[] ReadAvatar(string avatarPath)
    {
        return File.ReadAllBytes(avatarPath);
    }

    public string RelativeDirectory()
    {
        EnsureDirectory();
        return _root.RelativePath(StudentDirectory);
    }

    public void RemoveAll()
    {
        if (!Directory.Exists(StudentDirectory))
        {
            return;
        }

        if (!_root.IsInsideDataRoot(StudentDirectory))
        {
            throw new InvalidOperationException("Refusing to delete a student directory outside the .NET data root.");
        }

        // Controlled local delete: this is only used for explicit student data
        // cleanup inside the sanitized .NET data root.
        Directory.Delete(StudentDirectory, recursive: true);
    }

    public NetworkAuthHistoryDocument LoadNetworkAuthHistory()
    {
        return ReadJson<NetworkAuthHistoryDocument>(NetworkAuthHistoryPath) ?? new NetworkAuthHistoryDocument();
    }

    public string SaveNetworkAuthHistory(NetworkAuthHistoryDocument payload)
    {
        EnsureInfoDirectory();
        WriteJson(NetworkAuthHistoryPath, payload);
        return _root.RelativePath(NetworkAuthHistoryPath);
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(StudentDirectory);
    }

    private void EnsureInfoDirectory()
    {
        Directory.CreateDirectory(InfoDirectory);
    }

    private static T? ReadBusinessJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions); }
        // Unsupported or incomplete business files cannot restore a workspace.
        // Leave them untouched; only successful authentication writes fresh JSON.
        catch (JsonException) { return default; }
    }
    private T? ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return _encrypted.ReadJson<T>(path);
    }

    private Dictionary<string, object?> ReadJsonDictionary(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(_encrypted.ReadBytes(path));
        return JsonValueConverter.ToDictionary(document.RootElement.Clone());
    }

    private void WriteJson<T>(string path, T value)
    {
        _encrypted.WriteJson(path, value);
    }
}
