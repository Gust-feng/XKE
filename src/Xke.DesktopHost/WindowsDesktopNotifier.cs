using Xke.Core.Desktop;

namespace Xke.DesktopHost;

internal sealed class WindowsDesktopNotifier(Func<MainWindow?> window) : IDesktopNotifier
{
    public bool IsSupported => true;

    public async Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken)
    {
        var target = window();
        if (target is null || target.Dispatcher.HasShutdownStarted) return false;
        return await target.Dispatcher.InvokeAsync(() => canDeliver() && target.ShowLessonReminder(title, message),
            System.Windows.Threading.DispatcherPriority.Normal, cancellationToken).Task.ConfigureAwait(false);
    }
}
