using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Xke.Core.Network;

public sealed class ProcessNetworkCommandExecutor : INetworkCommandExecutor
{
    private static readonly Encoding OutputEncoding = ResolveOutputEncoding();

    // These Windows tools emit OEM console text, not UTF-8. Read the system code page, not the UI culture.
    private static Encoding ResolveOutputEncoding()
    {
        if (!OperatingSystem.IsWindows()) return Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(checked((int)GetOEMCP()));
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    public async Task<NetworkCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = OutputEncoding,
            StandardErrorEncoding = OutputEncoding,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return new NetworkCommandResult(
                process.ExitCode,
                await stdoutTask.ConfigureAwait(false),
                await stderrTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup: command timeout should become a normal API error, not a host crash.
            }

            return new NetworkCommandResult(-1, string.Empty, $"Command timed out: {fileName}");
        }
    }
}
