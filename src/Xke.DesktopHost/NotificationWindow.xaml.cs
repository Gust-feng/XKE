using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Xke.Core.Desktop;
using Xke.Storage;

namespace Xke.DesktopHost;

public partial class NotificationWindow : Window
{
    private readonly Action _open;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Func<bool> _valid;
    private DateTimeOffset _last = DateTimeOffset.UtcNow;
    private TimeSpan _remaining = TimeSpan.FromSeconds(9);
    private bool _hover;

    /// <summary>结构化课前提醒：字段直出，无文本解析。</summary>
    public NotificationWindow(DesktopLessonReminder reminder, Action open, Func<bool> valid, bool preview = false)
        : this(open, valid, preview)
    {
        CourseName.Text = reminder.Course.Length > 100 ? reminder.Course[..100] : reminder.Course;
        Room.Text = reminder.Room.Length > 100 ? reminder.Room[..100] : reminder.Room;
        Countdown.Text = reminder.MinutesUntilStart.ToString();
        HeroPanel.Visibility = Visibility.Visible;
    }

    /// <summary>通用通知回退：普通标题+正文。</summary>
    public NotificationWindow(string title, string message, Action open, Func<bool> valid, bool preview = false)
        : this(open, valid, preview)
    {
        Heading.Text = title.Length > 100 ? title[..100] : title;
        Message.Text = message.Length > 400 ? message[..400] : message;
        Kicker.Visibility = Visibility.Collapsed;
        SimplePanel.Visibility = Visibility.Visible;
    }

    private NotificationWindow(Action open, Func<bool> valid, bool preview)
    {
        InitializeComponent();
        _open = open;
        _valid = valid;
        if (preview) _remaining = TimeSpan.FromSeconds(30);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(handle, -20);
            SetWindowLong(handle, -20, style | 0x08000000 | 0x80); // No activation; omit task-switcher entry.
        };
        Loaded += (_, _) =>
        {
            PositionCard();
            _last = DateTimeOffset.UtcNow;
            _timer.Start();
        };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(PositionCard);
        _timer.Tick += (_, _) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (!_hover) _remaining -= now - _last;
            _last = now;
            if (!_valid() || _remaining <= TimeSpan.Zero) Close();
        };
        Closed += (_, _) => _timer.Stop();
    }
    private void PositionCard()
    {
        var host = System.Windows.Application.Current?.MainWindow ?? this;
        var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(host).Handle).WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var corner = transform.Transform(new System.Windows.Point(area.Right, area.Bottom));
        Left = corner.X - ActualWidth - 14;
        Top = corner.Y - ActualHeight - 14;
    }
    private void Pause(object sender, System.Windows.Input.MouseEventArgs e) => _hover = true;
    private void Resume(object sender, System.Windows.Input.MouseEventArgs e) { _hover = false; _last = DateTimeOffset.UtcNow; }
    private void Dismiss(object sender, RoutedEventArgs e) => Close();
    private void OpenApp(object sender, RoutedEventArgs e) { Close(); _open(); }
    private void OnCardOpen(object sender, System.Windows.Input.MouseButtonEventArgs e) { Close(); _open(); }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}

internal sealed class DesktopNotificationPresenter : IDisposable
{
    private sealed record Pending(string Identity, DesktopLessonReminder? Reminder, string Title, string Message,
        Func<bool> CanDeliver, CancellationToken Token, TaskCompletionSource<bool> Completion, bool Preview);
    private readonly Queue<Pending> _queue = new();
    private readonly XkeNotificationSettingsStore _store;
    private readonly Action _open;
    private NotificationWindow? _current;
    private string? _currentIdentity;
    private bool _disposed;
    public bool DoNotDisturb { get; private set; }
    public string? LastFailure { get; private set; }

    public DesktopNotificationPresenter(XkeDataRoot root, Action open)
    {
        _store = new(root);
        _open = open;
        DoNotDisturb = _store.LoadDoNotDisturb();
    }
    public void SetDoNotDisturb(bool value)
    {
        _store.SaveDoNotDisturb(value);
        DoNotDisturb = value;
        if (value) { ClearQueue(); _current?.Close(); }
    }
    public Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken token, bool preview = false)
        => Enqueue(new Pending(title + "\0" + message, null, title, message, canDeliver, token,
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), preview));

    public Task<bool> ShowReminderAsync(DesktopLessonReminder reminder, Func<bool> canDeliver, CancellationToken token, bool preview = false)
        => Enqueue(new Pending(Identity(reminder), reminder, string.Empty, string.Empty, canDeliver, token,
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), preview));

    private static string Identity(DesktopLessonReminder reminder)
        => $"lesson|{reminder.Date:yyyyMMdd}|{reminder.Start:HHmm}|{reminder.Course}|{reminder.Room}";

    private Task<bool> Enqueue(Pending pending)
    {
        LastFailure = null;
        if (_currentIdentity == pending.Identity) return Task.FromResult(pending.Preview);
        if (_disposed || pending.Token.IsCancellationRequested || !CanDeliver(pending.CanDeliver)) LastFailure = "提醒已取消";
        else if (DoNotDisturb) LastFailure = "免打扰已开启";
        else if (Suppressed(pending.Preview)) LastFailure = "当前系统状态暂不显示提醒";
        else if (_queue.Count >= 8 || _queue.Any(item => item.Identity == pending.Identity)) LastFailure = "已有提醒正在等待展示";
        if (LastFailure is not null) return Task.FromResult(false);
        _queue.Enqueue(pending);
        ShowNext();
        return pending.Completion.Task;
    }
    private void ShowNext()
    {
        if (_disposed || _current is not null) return;
        while (_queue.TryDequeue(out var item))
        {
            bool Valid() => !_disposed && !item.Token.IsCancellationRequested && !Suppressed(item.Preview) && CanDeliver(item.CanDeliver);
            if (!Valid()) { LastFailure = "提醒已取消或暂不适合展示"; item.Completion.TrySetResult(false); continue; }
            var card = item.Reminder is not null
                ? new NotificationWindow(item.Reminder, _open, Valid, item.Preview)
                : new NotificationWindow(item.Title, item.Message, _open, Valid, item.Preview);
            _current = card;
            _currentIdentity = item.Identity;
            card.Closed += (_, _) => { _current = null; _currentIdentity = null; ShowNext(); };
            try { card.Show(); item.Completion.TrySetResult(true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                _current = null; _currentIdentity = null; LastFailure = "提醒窗口暂时无法显示"; item.Completion.TrySetResult(false); continue;
            }
            return;
        }
    }
    private static bool CanDeliver(Func<bool> check)
    {
        try { return check(); }
        catch (OperationCanceledException) { return false; }
    }
    private bool Suppressed(bool preview) => DoNotDisturb || (SHQueryUserNotificationState(out var state) == 0
        && (preview ? state == 1 : state != 5));
    private void ClearQueue() { while (_queue.TryDequeue(out var item)) item.Completion.TrySetResult(false); }
    public void Dispose() { _disposed = true; ClearQueue(); _current?.Close(); }
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
}
