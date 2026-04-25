using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Security;

namespace Xke.Storage;

public sealed class XkeDesktopSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;
    private readonly LocalEncryptedFileStore _encrypted = new();

    public XkeDesktopSettingsStore(XkeDataRoot root)
    {
        _root = root;
    }

    public string SettingsPath => _root.DesktopSettingsPath;

    public DesktopSettingsDocument Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return CreateDefault();
        }

        try
        {
            var document = _encrypted.ReadJson<DesktopSettingsDocument>(SettingsPath);
            return Normalize(document);
        }
        catch
        {
            return CreateDefault();
        }
    }

    public string Save(DesktopSettingsDocument settings)
    {
        var normalized = Normalize(settings);
        _encrypted.WriteJson(SettingsPath, normalized);
        return _root.RelativePath(SettingsPath);
    }

    public static DesktopSettingsDocument CreateDefault()
    {
        return new DesktopSettingsDocument
        {
            LaunchAtStartup = false,
            SilentStartup = false,
            CloseToTray = true,
        };
    }

    public static DesktopSettingsDocument Normalize(DesktopSettingsDocument? raw)
    {
        var current = raw ?? CreateDefault();
        var launchAtStartup = current.LaunchAtStartup;
        return new DesktopSettingsDocument
        {
            LaunchAtStartup = launchAtStartup,
            SilentStartup = launchAtStartup && current.SilentStartup,
            CloseToTray = current.CloseToTray,
        };
    }
}
