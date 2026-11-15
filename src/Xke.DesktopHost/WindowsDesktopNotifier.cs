using Xke.Core.Desktop;

namespace Xke.DesktopHost;

internal sealed class WindowsDesktopNotifier(Func<MainWindow?> window) : IDesktopNotifier
{
    public bool IsSupported => true;

    public async Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken)
    {
        var target = window();
        if (target is null || target.Dispatcher.HasShutdownStarted) return false;
        var pending = await target.Dispatcher.InvokeAsync(() => target.ShowNotificationAsync(title, message, canDeliver, cancellationToken),
            System.Windows.Threading.DispatcherPriority.Normal, cancellationToken).Task.ConfigureAwait(false);
        return await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ShowLessonReminderAsync(DesktopLessonReminder reminder, Func<bool> canDeliver, CancellationToken cancellationToken)
    {
        var target = window();
        if (target is null || target.Dispatcher.HasShutdownStarted) return false;
        var pending = await target.Dispatcher.InvokeAsync(() => target.ShowLessonReminderAsync(reminder, canDeliver, cancellationToken),
            System.Windows.Threading.DispatcherPriority.Normal, cancellationToken).Task.ConfigureAwait(false);
        return await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
