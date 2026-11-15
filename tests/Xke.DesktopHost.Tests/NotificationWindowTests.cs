using System.Runtime.ExceptionServices;
using System.Windows;
using Xunit;

namespace Xke.DesktopHost.Tests;

public sealed class NotificationWindowTests
{
    [Fact]
    public void LessonReminderShowsCountdownAsHeroWithCourseRoomAndTime()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            NotificationWindow? window = null;
            try
            {
                var reminder = new Xke.Core.Desktop.DesktopLessonReminder("数据结构", "S2-207(新)", new DateOnly(2026, 10, 16), new TimeOnly(8, 0), 15);
                window = new NotificationWindow(reminder, () => { }, () => true, preview: true);
                window.Show();
                Assert.Equal("15", Assert.IsType<System.Windows.Documents.Run>(window.FindName("Countdown")).Text);
                Assert.Equal("数据结构", Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("CourseName")).Text);
                Assert.Equal("S2-207(新)", Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("Room")).Text);
                Assert.Equal(Visibility.Visible, Assert.IsType<System.Windows.Controls.Grid>(window.FindName("HeroPanel")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("SimplePanel")).Visibility);
                Assert.True(window.ActualWidth <= 420);
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Native notification did not settle");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void LessonReminderWithoutRoomLeavesRoomSpotEmpty()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            NotificationWindow? window = null;
            try
            {
                var reminder = new Xke.Core.Desktop.DesktopLessonReminder("数据结构", "", new DateOnly(2026, 10, 16), new TimeOnly(8, 0), 30);
                window = new NotificationWindow(reminder, () => { }, () => true, preview: true);
                window.Show();
                Assert.Equal(string.Empty, Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("Room")).Text);
                Assert.Equal("30", Assert.IsType<System.Windows.Documents.Run>(window.FindName("Countdown")).Text);
                Assert.Equal(Visibility.Visible, Assert.IsType<System.Windows.Controls.Grid>(window.FindName("HeroPanel")).Visibility);
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Native notification did not settle");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void GenericNotificationFallsBackToTitleAndMessage()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            NotificationWindow? window = null;
            try
            {
                window = new NotificationWindow("合成提醒", "只验证独立卡片窗口。", () => { }, () => true, preview: true);
                window.Show();
                Assert.Equal("合成提醒", Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("Heading")).Text);
                Assert.Equal("只验证独立卡片窗口。", Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("Message")).Text);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<System.Windows.Controls.Grid>(window.FindName("HeroPanel")).Visibility);
                Assert.Equal(Visibility.Visible, Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("SimplePanel")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("Kicker")).Visibility);
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Native notification did not settle");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void NotificationCanShowWithoutActivationOrTaskbarEntry()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            NotificationWindow? window = null;
            try
            {
                var reminder = new Xke.Core.Desktop.DesktopLessonReminder("数据结构", "S2-207(新)", new DateOnly(2026, 10, 16), new TimeOnly(8, 0), 15);
                window = new NotificationWindow(reminder, () => { }, () => true, preview: true);
                window.Show();
                Assert.True(window.IsVisible);
                Assert.False(window.ShowActivated);
                Assert.False(window.ShowInTaskbar);
                Assert.True(double.IsFinite(window.Left));
                Assert.True(double.IsFinite(window.Top));
                Assert.True(window.ActualWidth > 0);
                Assert.True(window.ActualHeight > 0);
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Native notification did not settle");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
