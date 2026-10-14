using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Xke.DesktopHost;

internal sealed class DesktopSingleInstance : IDisposable
{
    private Mutex? _mutex;

    private DesktopSingleInstance(Mutex mutex)
    {
        _mutex = mutex;
    }

    // Acquire and dispose on the WPF dispatcher thread; mutex ownership is thread-affine.
    public static DesktopSingleInstance? TryAcquire(string dataRootPath)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userId = identity.User!.Value;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRootPath)).ToUpperInvariant();
        var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}\n{root}")));
        var mutex = new Mutex(initiallyOwned: false, $@"Global\XKE.DesktopHost.{scope}");
        try
        {
            try
            {
                if (mutex.WaitOne(0))
                {
                    return new DesktopSingleInstance(mutex);
                }
            }
            catch (AbandonedMutexException)
            {
                // WaitOne granted ownership after the previous process exited unexpectedly.
                return new DesktopSingleInstance(mutex);
            }
        }
        catch
        {
            mutex.Dispose();
            throw;
        }

        mutex.Dispose();
        return null;
    }

    public static async Task<bool> WaitForActivationAsync(
        Func<CancellationToken, Task<bool>> tryActivate,
        TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            while (!deadline.IsCancellationRequested)
            {
                if (await tryActivate(deadline.Token).ConfigureAwait(false))
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
        }

        return false;
    }

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _mutex = null;
    }
}
