using System.Text.Json;

namespace Xke.Storage;

public sealed class XkeNotificationSettingsStore(XkeDataRoot root)
{
    private string PathName => Path.Combine(root.DesktopDirectory, "notification-settings.json");
    public bool LoadDoNotDisturb()
    {
        try
        {
            if (!File.Exists(PathName)) return false;
            return JsonSerializer.Deserialize<NotificationSettings>(File.ReadAllText(PathName))?.DoNotDisturb == true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }
    public void SaveDoNotDisturb(bool value) => AtomicFile.WriteText(PathName, JsonSerializer.Serialize(new NotificationSettings(value)));
    private sealed record NotificationSettings(bool DoNotDisturb);
}
