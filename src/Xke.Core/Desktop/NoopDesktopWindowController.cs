using Xke.Contracts.Responses;

namespace Xke.Core.Desktop;

public sealed class NoopDesktopWindowController : IDesktopWindowController
{
    public DesktopActivatePayload Activate()
    {
        return new DesktopActivatePayload(
            Activated: false,
            Visible: false,
            WindowState: new Dictionary<string, object?>
            {
                ["available"] = false,
                ["reason"] = "desktop-window-unavailable",
            });
    }
}
