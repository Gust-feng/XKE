using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Core.Releases;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class AppUpdateTests
{
    [Fact]
    public async Task ManualCheckDownloadAndExplicitRestart_AreDistinctAndNeverReplaceARunningApp()
    {
        var backend = new Backend(); var clock = new Clock(); var restartRequests = 0;
        using var service = new AppUpdateService(backend, clock, () => restartRequests++);
        Assert.Equal("idle", service.State.Status);
        Assert.Equal("available", (await service.CheckAsync(default)).Status);
        Assert.Equal(0, backend.Downloads); Assert.Equal(0, backend.Applies);
        Assert.Equal("ready", (await service.DownloadAsync(default)).Status); Assert.Equal(100, service.State.Progress);
        Assert.Equal(0, backend.Applies);
        Assert.Equal("installing", service.RequestApply().Status); Assert.Equal(1, restartRequests);
        Assert.Equal(0, backend.Applies); // The window must drain its host and reach the real exit boundary first.
        Assert.True(service.PrepareExit(false)); Assert.True(backend.Restart); Assert.Equal(1, backend.Applies);
    }

    [Fact]
    public async Task AutomaticModeChecksDownloadsButWaitsForExit_AndDisabledModeDoesNothing()
    {
        var backend = new Backend(); var clock = new Clock(); using var service = new AppUpdateService(backend, clock);
        var enabled = false;
        clock.Now += TimeSpan.FromMinutes(1); await service.RunAutomaticAsync(() => enabled, default);
        Assert.Equal(0, backend.Checks);
        enabled = true; await service.RunAutomaticAsync(() => enabled, default);
        Assert.Equal("ready", service.State.Status); Assert.Equal(1, backend.Downloads); Assert.Equal(0, backend.Applies);
        Assert.False(service.PrepareExit(false)); Assert.True(service.PrepareExit(true)); Assert.False(backend.Restart);
    }

    [Fact]
    public async Task ManualCheckIsAutomaticallyDownloadedOnNextTick_NotSixHoursLater()
    {
        var backend = new Backend(); var clock = new Clock(); using var service = new AppUpdateService(backend, clock);
        await service.CheckAsync(default); await service.RunAutomaticAsync(() => true, default);
        Assert.Equal("ready", service.State.Status); Assert.Equal(1, backend.Checks); Assert.Equal(1, backend.Downloads);
    }

    [Fact]
    public async Task NoNewRelease_IsNotAnError_AndAutomaticChecksAreThrottled()
    {
        var backend = new Backend { Latest = null }; var clock = new Clock(); using var service = new AppUpdateService(backend, clock);
        clock.Now += TimeSpan.FromMinutes(1);
        await service.RunAutomaticAsync(() => true, default); await service.RunAutomaticAsync(() => true, default);
        Assert.Equal("up_to_date", service.State.Status); Assert.Equal(1, backend.Checks); Assert.Equal(0, backend.Downloads);
        clock.Now += TimeSpan.FromHours(6); await service.RunAutomaticAsync(() => true, default); Assert.Equal(2, backend.Checks);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("download")]
    public async Task FailurePreservesCurrentVersionAndNeverApplies(string phase)
    {
        var backend = new Backend(); var clock = new Clock(); using var service = new AppUpdateService(backend, clock);
        if (phase == "check") backend.FailCheck = true; else backend.FailDownload = true;
        await service.CheckAsync(default); if (phase == "download") await service.DownloadAsync(default);
        Assert.Equal("error", service.State.Status); Assert.Equal("1.0.0", service.State.CurrentVersion);
        Assert.DoesNotContain("synthetic-private-diagnostic", service.State.Error);
        Assert.False(service.PrepareExit(true)); Assert.Equal(0, backend.Applies);
        if (phase == "download")
        { backend.FailDownload = false; clock.Now += TimeSpan.FromHours(7); await service.RunAutomaticAsync(() => true, default); Assert.Equal("ready", service.State.Status); }
    }

    [Fact]
    public async Task UnsupportedBuildDoesNotContactNetworkOrPretendToInstall()
    {
        var backend = new Backend { Installed = false }; using var service = new AppUpdateService(backend, new Clock());
        await service.CheckAsync(default); await service.DownloadAsync(default); service.RequestApply(); await service.RunAutomaticAsync(() => true, default);
        Assert.Equal("unsupported", service.State.Status); Assert.Equal(0, backend.Checks); Assert.False(service.PrepareExit(true));
    }

    [Fact]
    public async Task DisablingAutomaticDownloadCancelsIt_WithoutCancellingAnExplicitManualDownload()
    {
        var backend = new Backend { HoldDownload = true }; var clock = new Clock(); using var service = new AppUpdateService(backend, clock);
        clock.Now += TimeSpan.FromMinutes(1);
        var automatic = service.RunAutomaticAsync(() => true, default);
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); service.SetAutomaticEnabled(false); await automatic;
        Assert.Equal("available", service.State.Status); Assert.Null(backend.PendingVersion); Assert.False(service.PrepareExit(false));
        backend.HoldDownload = false; await service.DownloadAsync(default); Assert.Equal("ready", service.State.Status);
    }

    [Fact]
    public async Task ConcurrentChecksDoNotOverlap_AndCancellationReleasesTheGate()
    {
        var backend = new Backend { HoldDownload = true }; using var service = new AppUpdateService(backend, new Clock());
        await service.CheckAsync(default); using var cancellation = new CancellationTokenSource();
        var download = service.DownloadAsync(cancellation.Token); await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await service.CheckAsync(default); Assert.Equal(1, backend.Checks); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        backend.HoldDownload = false; await service.DownloadAsync(default); Assert.Equal("ready", service.State.Status);
    }

    [Fact]
    public async Task ManualDownloadOutlivesItsRequest_AndOnlyRealShutdownCancelsIt()
    {
        var backend = new Backend { HoldDownload = true }; using var service = new AppUpdateService(backend, new Clock());
        await service.CheckAsync(default);
        Assert.Equal("downloading", service.RequestDownload().Status);
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        service.SetAutomaticEnabled(false); Assert.Equal("downloading", service.State.Status);
        await service.StopAsync(); Assert.Equal("available", service.State.Status); Assert.Null(backend.PendingVersion);
        service.Resume(); backend.HoldDownload = false; Assert.Equal("ready", service.RequestDownload().Status);
    }

    [Theory]
    [InlineData("https://github.com/Gust-feng/XKE", true)]
    [InlineData("https://github.com/Gust-feng/XKE/releases/tag/v1.0.0", true)]
    [InlineData("https://github.com/Gust-feng/XKE-other", false)]
    [InlineData("https://github.com/other/project", false)]
    [InlineData("http://github.com/Gust-feng/XKE", false)]
    [InlineData("https://github.com:8443/Gust-feng/XKE", false)]
    [InlineData("https://user@github.com/Gust-feng/XKE", false)]
    [InlineData("file:///Gust-feng/XKE", false)]
    public void ExternalLinksStayWithinTheVerifiedProject(string uri, bool allowed)
        => Assert.Equal(allowed, AppRelease.IsProjectPage(new Uri(uri)));

    [Fact]
    public async Task UpdateApiNeedsNativeProofButNotSchoolLogin_AndMachinePreferencePersists()
    {
        using var temp = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var backend = new Backend(); using var service = new AppUpdateService(backend, new Clock(), () => { });
        await using var host = new XkeWebHostService(new() { HostMode = "desktop-webview", AppUpdates = service,
            DesktopRequestToken = "synthetic-proof", AuthDataRootPath = temp.RootPath, WebRootPath = web.RootPath,
            EnableBackgroundTasks = false, ScheduleApiOptions = new(), Urls = ["http://127.0.0.1:0"] });
        var launch = await host.StartAsync(); using var client = new HttpClient { BaseAddress = new(launch.BaseUrl) };
        using var rejected = await client.PostAsJsonAsync("/api/app/updates/check", new { }); Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        client.DefaultRequestHeaders.Add(LocalApiAccessPolicy.DesktopTokenHeaderName, "synthetic-proof");
        using var check = await client.PostAsJsonAsync("/api/app/updates/check", new { }); Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        using var download = await client.PostAsJsonAsync("/api/app/updates/download", new { }); Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        using var read = JsonDocument.Parse(await client.GetStringAsync("/api/app/updates"));
        Assert.Equal("ready", read.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal("1.0.0", read.RootElement.GetProperty("data").GetProperty("current_version").GetString());
        using var settings = await client.PostAsJsonAsync("/api/desktop/settings", new { auto_update_enabled = false }); Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        var root = new Xke.Storage.XkeDataRoot(temp.RootPath); Assert.False(root.DesktopSettings().Load().AutoUpdateEnabled); Assert.Equal(0, root.Sessions().Count());
    }

    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Backend : IAppUpdateBackend
    {
        public bool Installed = true, FailCheck, FailDownload, HoldDownload;
        public string? Latest = "1.1.0";
        public int Checks, Downloads, Applies; public bool Restart;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsInstalled => Installed;
        public string? CurrentVersion => "1.0.0";
        public string? PendingVersion { get; private set; }
        public Task<string?> CheckAsync(CancellationToken token)
        { Checks++; if (FailCheck) throw new IOException("synthetic-private-diagnostic"); return Task.FromResult(Latest); }
        public async Task DownloadAsync(Action<int> progress, CancellationToken token)
        { Downloads++; Entered.TrySetResult(); if (HoldDownload) await Task.Delay(Timeout.Infinite, token); if (FailDownload) throw new IOException("synthetic-private-diagnostic"); progress(40); progress(100); PendingVersion = Latest; }
        public void ApplyOnExit(bool restart) { Applies++; Restart = restart; }
    }
}
