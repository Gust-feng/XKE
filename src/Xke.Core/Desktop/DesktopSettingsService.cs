using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Desktop;

public sealed class DesktopSettingsService
{
    private readonly XkeDataRoot _dataRoot;
    private readonly IDesktopStartupRegistrar _startupRegistrar;

    public DesktopSettingsService(XkeDataRoot dataRoot, IDesktopStartupRegistrar startupRegistrar)
    {
        _dataRoot = dataRoot;
        _startupRegistrar = startupRegistrar;
    }

    public (string Message, DesktopSettingsPayload Payload) GetSettings()
    {
        var settings = _dataRoot.DesktopSettings().Load();
        return ("桌面设置已加载", new DesktopSettingsPayload(settings, StartupRegistered: _startupRegistrar.GetRegistrationState()));
    }

    public (string Message, DesktopSettingsPayload Payload) UpdateSettings(DesktopSettingsRequest request)
    {
        var current = _dataRoot.DesktopSettings().Load();
        var next = XkeDesktopSettingsStore.Normalize(new DesktopSettingsDocument
        {
            AutoUpdateEnabled = request.AutoUpdateEnabled ?? current.AutoUpdateEnabled,
            LaunchAtStartup = request.LaunchAtStartup ?? current.LaunchAtStartup,
            SilentStartup = request.SilentStartup ?? current.SilentStartup,
            CloseToTray = request.CloseToTray ?? current.CloseToTray,
        });

        if (request.LaunchAtStartup.HasValue)
        {
            _startupRegistrar.SetRegistrationState(next.LaunchAtStartup);
        }

        var startupRegistered = _startupRegistrar.GetRegistrationState();
        if (request.LaunchAtStartup.HasValue
            && startupRegistered.HasValue
            && startupRegistered.Value != next.LaunchAtStartup)
        {
            throw new InvalidOperationException(next.LaunchAtStartup
                ? "开机自启注册未生效。"
                : "开机自启注销未生效。");
        }

        _dataRoot.DesktopSettings().Save(next);
        return ("桌面设置已保存", new DesktopSettingsPayload(next, StartupRegistered: startupRegistered));
    }
}
