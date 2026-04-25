using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Core.Notice;
using Xke.Core.Update;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class UpdateWebHostTests
{
    [Fact]
    public async Task FrontendFacingConfigAndFeed_DoNotExposeDownloadDetails()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-update-sanitize");
        var package = BuildZipPackage(("Xke.DesktopHost.exe", "new"));
        var remote = new StaticNoticeRemoteSource(BuildCloudFeed(package));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            noticeRemoteSource: remote);

        var config = await host.Client.GetStringAsync("/api/config?version=1.0.0&channel=beta");
        var feed = await host.Client.GetStringAsync("/api/updates/feed?student_id=20260001&trigger=manual");

        Assert.DoesNotContain("download_url", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("package_sha256", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("package_size", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://download.example.invalid", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("download_url", feed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("package_sha256", feed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("package_size", feed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://download.example.invalid", feed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateDownload_UsesHiddenUrlAndVerifiesPackage()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-update-download");
        var package = BuildZipPackage(("Xke.DesktopHost.exe", "new"));
        var downloader = new CapturingUpdatePackageDownloader(package);

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            noticeRemoteSource: new StaticNoticeRemoteSource(BuildCloudFeed(package)),
            updatePackageDownloader: downloader,
            updateVersionProvider: new FixedUpdateVersionProvider("1.0.0"));

        var statusResponse = await host.Client.GetAsync("/api/update/status");
        var statusText = await statusResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.DoesNotContain("download_url", statusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://download.example.invalid", statusText, StringComparison.OrdinalIgnoreCase);

        var downloadResponse = await host.Client.PostAsJsonAsync("/api/update/download", new { });
        var downloadDocument = await WebHostTestJson.ReadJsonAsync(downloadResponse);
        var jobId = WebHostTestJson.GetData(downloadDocument).GetProperty("job_id").GetString();
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        var job = await WaitForJobAsync(host.Client, jobId!);
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        var packageFiles = Directory.GetFiles(root.UpdatePackagesDirectory, "*.zip", SearchOption.TopDirectoryOnly);

        Assert.True(
            string.Equals("verified", job.GetProperty("status").GetString(), StringComparison.Ordinal),
            job.ToString());
        Assert.Equal(new Uri("https://download.example.invalid/xke-1.2.3.zip"), downloader.RequestedUri);
        Assert.Single(packageFiles);
        Assert.StartsWith(root.UpdateDirectory, packageFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareInstall_OnlySucceedsAfterVerifiedPackage()
    {
        using var webRoot = new TemporaryWebRoot();
        using var dataRoot = new TemporaryDirectory("xke-update-prepare");
        var package = BuildZipPackage(("Xke.DesktopHost.exe", "new"));

        await using var host = await WebHostTestServer.StartAsync(
            webRoot,
            dataRoot,
            hostMode: "desktop-webview-test",
            noticeRemoteSource: new StaticNoticeRemoteSource(BuildCloudFeed(package)),
            updatePackageDownloader: new CapturingUpdatePackageDownloader(package),
            updateVersionProvider: new FixedUpdateVersionProvider("1.0.0"));

        var beforeDownload = await host.Client.PostAsJsonAsync("/api/update/prepare-install", new { });
        Assert.Equal(HttpStatusCode.BadRequest, beforeDownload.StatusCode);

        var downloadResponse = await host.Client.PostAsJsonAsync("/api/update/download", new { });
        var downloadDocument = await WebHostTestJson.ReadJsonAsync(downloadResponse);
        var jobId = WebHostTestJson.GetData(downloadDocument).GetProperty("job_id").GetString()!;
        await WaitForJobAsync(host.Client, jobId);

        var prepare = await host.Client.PostAsJsonAsync("/api/update/prepare-install", new { });
        var prepareDocument = await WebHostTestJson.ReadJsonAsync(prepare);
        var payload = WebHostTestJson.GetData(prepareDocument);
        var root = new XkeDataRoot(dataRoot.RootPath, dataRoot.RootPath);
        var plan = root.Updates().LoadInstallPlan();

        Assert.Equal(HttpStatusCode.OK, prepare.StatusCode);
        Assert.True(payload.GetProperty("prepared").GetBoolean());
        Assert.True(File.Exists(root.UpdateInstallPlanPath));
        Assert.Equal(jobId, plan.JobId);
        Assert.Equal("prepared", plan.Status);
    }

    [Fact]
    public async Task UpdateInstaller_ReplacesProgramFilesAndKeepsLocalData()
    {
        using var workspace = new TemporaryDirectory("xke-update-install-workspace");
        var installDir = Path.Combine(workspace.RootPath, "app");
        var dataDir = Path.Combine(workspace.RootPath, ".runtime", "data");
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(Path.Combine(installDir, ".runtime", "data"));
        Directory.CreateDirectory(Path.Combine(installDir, ".desktop", "webview-data"));
        File.WriteAllText(Path.Combine(installDir, "Xke.DesktopHost.exe"), "old-main");
        File.WriteAllText(Path.Combine(installDir, "old.txt"), "old");
        File.WriteAllText(Path.Combine(installDir, ".runtime", "data", "student.json"), "keep");
        File.WriteAllText(Path.Combine(installDir, ".desktop", "webview-data", "state"), "keep");

        var package = BuildZipPackage(
            ("Xke.DesktopHost.exe", "new-main"),
            ("new.txt", "new"),
            (".runtime/data/should-not-copy.txt", "bad"));
        var root = XkeDataRootFactory.CreateDefault(workspace.RootPath, workspace.RootPath, dataDir);
        Directory.CreateDirectory(root.UpdatePackagesDirectory);
        var packagePath = root.Updates().ResolvePackagePath("xke-1.2.3-test.zip");
        await File.WriteAllBytesAsync(packagePath, package);
        var plan = new XkeUpdateInstallPlanDocument
        {
            JobId = "job-1",
            LatestVersion = "1.2.3",
            PackagePath = packagePath,
            PackageSha256 = Sha256Hex(package),
            PackageSize = package.Length,
            InstallDirectory = installDir,
            AppExecutablePath = Path.Combine(installDir, "Xke.DesktopHost.exe"),
            BackupDirectory = root.Updates().ResolveBackupDirectory("job-1"),
            Status = "prepared",
            CreatedAt = DateTimeOffset.Now.ToString("O"),
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        root.Updates().SaveInstallPlan(plan);

        var result = await new UpdateInstaller().InstallAsync(new UpdateInstallerOptions(
            ParentProcessId: 0,
            PlanPath: root.UpdateInstallPlanPath,
            MainExecutablePath: plan.AppExecutablePath,
            RestartAfterInstall: false,
            WaitTimeout: TimeSpan.FromMilliseconds(1)));
        var restoredPlan = root.Updates().LoadInstallPlan();

        Assert.True(result.Ok);
        Assert.Equal("new-main", File.ReadAllText(Path.Combine(installDir, "Xke.DesktopHost.exe")));
        Assert.True(File.Exists(Path.Combine(installDir, "new.txt")));
        Assert.False(File.Exists(Path.Combine(installDir, "old.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(installDir, ".runtime", "data", "student.json")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(installDir, ".desktop", "webview-data", "state")));
        Assert.False(File.Exists(Path.Combine(installDir, ".runtime", "data", "should-not-copy.txt")));
        Assert.Equal("installed", restoredPlan.Status);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.2.0", "1.1.9", 1)]
    [InlineData("v1.2.0", "1.2.0", 0)]
    public void VersionComparer_HandlesNumericTokens(string left, string right, int expected)
    {
        Assert.Equal(expected, UpdateVersionComparer.Compare(left, right));
    }

    [Fact]
    public void UpdateManifestParser_RejectsNonHttpsAndBadHash()
    {
        var manifest = UpdateManifestParser.FromUpdateInfo(new Dictionary<string, object?>
        {
            ["latest_version"] = "1.2.3",
            ["download_url"] = "http://example.invalid/xke.zip",
            ["package_sha256"] = "bad",
            ["package_type"] = "zip",
        });

        Assert.False(manifest.HasUpdatePackage);
        Assert.Contains(manifest.ValidationErrors, item => item.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.ValidationErrors, item => item.Contains("package_sha256", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<JsonElement> WaitForJobAsync(HttpClient client, string jobId)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var response = await client.GetAsync($"/api/update/jobs/{jobId}");
            var document = await WebHostTestJson.ReadJsonAsync(response);
            var job = WebHostTestJson.GetData(document);
            var status = job.GetProperty("status").GetString();
            if (status is "verified" or "failed")
            {
                return job.Clone();
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("更新任务未完成");
    }

    private static Dictionary<string, byte[]> BuildCloudFeed(byte[] package)
    {
        var feed = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["updated_at"] = "2026-04-25T08:00:00+08:00",
            ["announcement"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["items"] = new List<object?>(),
            },
            ["policy"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["update_info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updated_at"] = "2026-04-25T08:00:00+08:00",
                ["latest_version"] = "1.2.3",
                ["min_supported_version"] = "1.0.0",
                ["download_url"] = "https://download.example.invalid/xke-1.2.3.zip",
                ["package_sha256"] = Sha256Hex(package),
                ["package_size"] = package.Length,
                ["package_type"] = "zip",
                ["release_notes_url"] = "https://download.example.invalid/releases/1.2.3",
                ["force_upgrade"] = false,
            },
        };
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cloud-feed.json"] = JsonSerializer.SerializeToUtf8Bytes(feed, new JsonSerializerOptions { WriteIndented = true }),
        };
    }

    private static byte[] BuildZipPackage(params (string Path, string Content)[] files)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = zip.CreateEntry(file.Path.Replace('\\', '/'));
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }

        return memory.ToArray();
    }

    private static string Sha256Hex(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private sealed class StaticNoticeRemoteSource : INoticeRemoteSource
    {
        private readonly Dictionary<string, byte[]> _files;

        public StaticNoticeRemoteSource(Dictionary<string, byte[]> files)
        {
            _files = files;
        }

        public Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var normalized = (relativePath ?? string.Empty).Trim().TrimStart('/').Replace('\\', '/');
            return _files.TryGetValue(normalized, out var content)
                ? Task.FromResult(new NoticeRemoteContentResult(true, content, string.Empty))
                : Task.FromResult(new NoticeRemoteContentResult(false, Array.Empty<byte>(), $"missing: {normalized}"));
        }
    }

    private sealed class CapturingUpdatePackageDownloader : IUpdatePackageDownloader
    {
        private readonly byte[] _content;

        public CapturingUpdatePackageDownloader(byte[] content)
        {
            _content = content;
        }

        public Uri? RequestedUri { get; private set; }

        public async Task DownloadAsync(
            Uri packageUri,
            string destinationPath,
            Func<long, long?, CancellationToken, Task> reportProgress,
            CancellationToken cancellationToken = default)
        {
            RequestedUri = packageUri;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await reportProgress(0, _content.Length, cancellationToken);
            await File.WriteAllBytesAsync(destinationPath, _content, cancellationToken);
            await reportProgress(_content.Length, _content.Length, cancellationToken);
        }
    }

    private sealed class FixedUpdateVersionProvider : IUpdateVersionProvider
    {
        public FixedUpdateVersionProvider(string currentVersion)
        {
            CurrentVersion = currentVersion;
        }

        public string CurrentVersion { get; }
    }
}
