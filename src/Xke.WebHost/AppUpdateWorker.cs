using Microsoft.Extensions.Hosting;
using Xke.Core.Releases;
using Xke.Storage;

namespace Xke.WebHost;

internal sealed class AppUpdateWorker(AppUpdateService updates, XkeDataRoot data) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        updates.Resume();
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var drain = updates.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await drain.ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!updates.IsInstalled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await updates.RunAutomaticAsync(() => data.DesktopSettings().Load().AutoUpdateEnabled, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
