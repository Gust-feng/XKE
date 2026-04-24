using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Desktop;

public sealed class DesktopSettingsService
{
    private readonly XkeDataRoot _dataRoot;

    public DesktopSettingsService(XkeDataRoot dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public (string Message, DesktopSettingsPayload Payload) GetSettings()
    {
        var settings = _dataRoot.DesktopSettings().Load();
        return ("桌面设置已加载", new DesktopSettingsPayload(settings, StartupRegistered: null));
    }

    public (string Message, DesktopSettingsPayload Payload) UpdateSettings(DesktopSettingsRequest request)
    {
        var current = _dataRoot.DesktopSettings().Load();
        var next = XkeDesktopSettingsStore.Normalize(new DesktopSettingsDocument
        {
            LaunchAtStartup = request.LaunchAtStartup ?? current.LaunchAtStartup,
            SilentStartup = request.SilentStartup ?? current.SilentStartup,
            CloseToTray = request.CloseToTray ?? current.CloseToTray,
        });

        _dataRoot.DesktopSettings().Save(next);
        return ("桌面设置已保存", new DesktopSettingsPayload(next, StartupRegistered: null));
    }
}
