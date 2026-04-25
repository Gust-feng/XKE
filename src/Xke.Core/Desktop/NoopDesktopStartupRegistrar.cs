namespace Xke.Core.Desktop;

public sealed class NoopDesktopStartupRegistrar : IDesktopStartupRegistrar
{
    public bool? GetRegistrationState()
    {
        return null;
    }

    public void SetRegistrationState(bool enabled)
    {
        if (enabled)
        {
            throw new InvalidOperationException("当前宿主不支持注册开机自启。");
        }
    }
}
