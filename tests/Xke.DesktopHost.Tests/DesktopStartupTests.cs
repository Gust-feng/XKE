using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.DesktopHost.Tests;

public sealed class DesktopStartupTests
{
    [Fact]
    public void NormalInitializationDoesNotExposeTheErrorExitAction() => WithWindow(window =>
    {
        var exit = (Button)window.FindName("FallbackExitButton");
        Assert.Equal(Visibility.Collapsed, exit.Visibility);
        Call(window, "ShowInitializationState");
        Assert.Equal(Visibility.Collapsed, exit.Visibility);
        Assert.Equal(Visibility.Collapsed, ((Border)window.FindName("ErrorPanel")).Visibility);
    });

    [Fact]
    public void FailureShowsRecoveryAndRetryKeepsThatSurfaceVisible() => WithWindow(window =>
    {
        Call(window, "ShowErrorState", "synthetic failure", "synthetic detail", false);
        Assert.Equal(Visibility.Visible, ((Button)window.FindName("FallbackExitButton")).Visibility);
        Assert.Equal(Visibility.Visible, ((Border)window.FindName("ErrorPanel")).Visibility);
        Call(window, "ShowInitializationState");
        Assert.Equal(Visibility.Visible, ((Button)window.FindName("FallbackExitButton")).Visibility);
        Assert.Equal(Visibility.Visible, ((Border)window.FindName("ErrorPanel")).Visibility);
        Call(window, "ShowWebViewState");
        Assert.Equal(Visibility.Collapsed, ((Button)window.FindName("FallbackExitButton")).Visibility);
        Assert.Equal(Visibility.Collapsed, ((Border)window.FindName("ErrorPanel")).Visibility);
    });

    [Fact]
    public void ContentReadinessReleasesOnlyThePendingStartup() => WithWindow(window =>
    {
        Assert.Equal(false, Call(window, "HandleBridgeCall", "content_ready", Array.Empty<string>()));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        typeof(MainWindow).GetField("_frontendReady", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, pending);
        Assert.Equal(true, Call(window, "HandleBridgeCall", "content_ready", Array.Empty<string>()));
        Assert.True(pending.Task.IsCompletedSuccessfully);
        Assert.Equal(false, Call(window, "HandleBridgeCall", "content_ready", Array.Empty<string>()));
    });

    private static object? Call(MainWindow window, string name, params object[] args)
        => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static void WithWindow(Action<MainWindow> check)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xke-startup-test-" + Guid.NewGuid().ToString("N"));
                window = new MainWindow(new XkeWebHostService(), new XkeDataRoot(path));
                check(window);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (window is not null)
                {
                    typeof(MainWindow).GetField("_hostStopped", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
                    window.Close();
                }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Synthetic native startup test did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
