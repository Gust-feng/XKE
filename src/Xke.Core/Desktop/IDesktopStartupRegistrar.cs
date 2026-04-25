namespace Xke.Core.Desktop;

public interface IDesktopStartupRegistrar
{
    bool? GetRegistrationState();

    void SetRegistrationState(bool enabled);
}
