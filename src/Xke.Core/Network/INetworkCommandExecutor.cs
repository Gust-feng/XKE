namespace Xke.Core.Network;

public sealed record NetworkCommandResult(int ExitCode, string Stdout, string Stderr);

public interface INetworkCommandExecutor
{
    Task<NetworkCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
