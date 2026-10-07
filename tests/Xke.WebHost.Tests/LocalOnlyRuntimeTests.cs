using Microsoft.Extensions.DependencyInjection;
using Xke.Core.Feedback;
using Xke.Core.Telemetry;
using Xke.Core.Notice;
using Xke.WebHost;
using Xke.Core.Update;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class LocalOnlyRuntimeTests
{
    [Fact]
    public async Task LocalOnlyHost_UsesLocalTransportsEvenWhenCloudEndpointsAreConfigured()
    {
        using var dataRoot = new TemporaryDirectory();
        using var webRoot = new TemporaryWebRoot();
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            LocalOnly = true,
            AuthDataRootPath = dataRoot.RootPath,
            WebRootPath = webRoot.RootPath,
            TelemetryCloudEndpoint = "https://example.test/telemetry",
            FeedbackCloudEndpoint = "https://example.test/feedback",
        });

        Assert.IsType<LocalOnlyNoticeRemoteSource>(app.Services.GetRequiredService<INoticeRemoteSource>());
        Assert.IsType<LocalOnlyUpdatePackageDownloader>(app.Services.GetRequiredService<IUpdatePackageDownloader>());
        Assert.IsType<LocalOnlyTelemetryTransport>(app.Services.GetRequiredService<ITelemetryTransport>());
        Assert.IsType<LocalFeedbackOutboxTransport>(app.Services.GetRequiredService<IFeedbackTransport>());
    }

    [Fact]
    public async Task LocalOnlyNoticeSource_NeverFetchesRemoteContent()
    {
        var result = await new LocalOnlyNoticeRemoteSource().GetBytesAsync("cloud-feed.json");

        Assert.False(result.Success);
        Assert.Empty(result.Content);
        Assert.Contains("本地开发模式", result.Error);
    }

    [Fact]
    public async Task LocalOnlyDownloader_RejectsRemotePackages()
    {
        var downloader = new LocalOnlyUpdatePackageDownloader();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(
            new Uri("https://example.test/package.zip"),
            Path.Combine(Path.GetTempPath(), "xke-local-only-test.zip"),
            (_, _, _) => Task.CompletedTask));

        Assert.Contains("本地开发模式", error.Message);
    }
}
