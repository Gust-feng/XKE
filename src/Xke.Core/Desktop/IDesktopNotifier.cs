namespace Xke.Core.Desktop;

public interface IDesktopNotifier
{
    bool IsSupported { get; }
    Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken);
}

public sealed class NoopDesktopNotifier : IDesktopNotifier
{
    public bool IsSupported => false;
    public Task<bool> ShowAsync(string title, string message, Func<bool> canDeliver, CancellationToken cancellationToken) => Task.FromResult(false);
}
