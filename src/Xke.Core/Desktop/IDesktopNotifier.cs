namespace Xke.Core.Desktop;

/// <summary>结构化课前提醒：桌面卡片直接消费字段，不经过文本拼装/解析。</summary>
public sealed record DesktopLessonReminder(string Course, string Room, DateOnly Date, TimeOnly Start, int MinutesUntilStart);

public interface IDesktopNotifier
{
    bool IsSupported { get; }
    Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken);
    Task<bool> ShowLessonReminderAsync(DesktopLessonReminder reminder, Func<bool> canDeliver, CancellationToken cancellationToken);
}

public sealed class NoopDesktopNotifier : IDesktopNotifier
{
    public bool IsSupported => false;
    public Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> ShowLessonReminderAsync(DesktopLessonReminder reminder, Func<bool> canDeliver, CancellationToken cancellationToken) => Task.FromResult(false);
}
