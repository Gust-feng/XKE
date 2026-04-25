using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.SecurityStorage.Tests;

public sealed class AuthStorageTests
{
    [Fact]
    public void ProtectedJsonFileStore_RoundTripsPayload()
    {
        using var workspace = new TemporaryDirectory();
        var filePath = Path.Combine(workspace.RootPath, "secret.json.dpapi");
        var store = new ProtectedJsonFileStore();

        store.Write(filePath, new StoredCredentialDocument
        {
            User = "20260001",
            Password = "secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-token",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });

        var restored = store.Read<StoredCredentialDocument>(filePath);

        Assert.NotNull(restored);
        Assert.Equal("20260001", restored!.User);
        Assert.Equal("secret", restored.Password);
        Assert.DoesNotContain("secret", File.ReadAllText(filePath));
    }

    [Fact]
    public void DataRootFactory_DefaultsToLocalAppDataXkeDirectory()
    {
        using var workspace = new TemporaryDirectory();
        var previousDotNet = Environment.GetEnvironmentVariable("XKE_DOTNET_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XKE_DOTNET_DATA_DIR", null);
            var root = XkeDataRootFactory.CreateDefault(workspace.RootPath);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            Assert.Equal(Path.Combine(localAppData, ".XKE", "data"), root.RootPath);
            Assert.False(root.RootPath.StartsWith(workspace.RootPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XKE_DOTNET_DATA_DIR", previousDotNet);
        }
    }

    [Fact]
    public void AuthStores_PersistCredentialsSessionsAndStudentFiles()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var credentialStore = root.Credentials();
        var sessionStore = root.Sessions();
        var studentStore = root.ForStudent("20260001");

        credentialStore.Save(new StoredCredentialDocument
        {
            User = "20260001",
            Password = "secret",
            StudentId = "20260001",
            TrustedLoginToken = "trusted-token",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });

        studentStore.SaveProfile(new StudentProfileDocument
        {
            StudentId = "20260001",
            Name = "测试同学",
            AcademyName = "信息工程学院",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });

        studentStore.SavePreferences(new AppSettingsDocument
        {
            Appearance = new AppearanceSettingsDocument { Theme = "forest-notes" },
            Auth = new AuthSettingsDocument { PreloginMode = "auto" },
        });

        studentStore.SaveAvatar("avatar.png", new byte[] { 1, 2, 3, 4 });
        var session = sessionStore.Create("20260001", "20260001", "auto");

        var restoredCredential = credentialStore.Load("20260001");
        var restoredProfile = studentStore.LoadProfile();
        var restoredSettings = studentStore.LoadPreferences();
        var restoredSession = sessionStore.Load(session.SessionId);

        Assert.NotNull(restoredCredential);
        Assert.NotNull(restoredProfile);
        Assert.NotNull(restoredSettings);
        Assert.NotNull(restoredSession);
        Assert.Equal("trusted-token", restoredCredential!.TrustedLoginToken);
        Assert.Equal("测试同学", restoredProfile!.Name);
        Assert.Equal("forest-notes", restoredSettings!.Appearance.Theme);
        Assert.Equal("auto", restoredSession!.PreloginMode);
        Assert.NotNull(studentStore.GetAvatarPath());
        AssertEncryptedFileDoesNotContain(studentStore.ProfilePath, "测试同学");
        AssertEncryptedFileDoesNotContain(studentStore.PreferencesPath, "forest-notes");
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(studentStore.GetAvatarPath()!));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, studentStore.ReadAvatar(studentStore.GetAvatarPath()!));

        sessionStore.Remove(session.SessionId);
        Assert.Null(sessionStore.Load(session.SessionId));
    }

    [Fact]
    public void RuntimeDataRoot_UsesCloudSyncAndDesktopPaths()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);

        Assert.Equal(Path.Combine(root.RootPath, ".cloud"), root.CloudDirectory);
        Assert.Equal(Path.Combine(root.RootPath, ".cloud", "feed.json"), root.CloudFeedPath);
        Assert.Equal(Path.Combine(root.RootPath, ".cloud", "feed.meta.json"), root.CloudFeedMetaPath);
        Assert.Equal(Path.Combine(root.RootPath, ".desktop", "desktop_settings.json"), root.DesktopSettingsPath);
        Assert.Contains($"{Path.DirectorySeparatorChar}.runtime{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}.cloud", root.CloudDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesktopSettingsStore_PersistsNormalizedSettings()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var store = root.DesktopSettings();

        store.Save(new DesktopSettingsDocument
        {
            LaunchAtStartup = false,
            SilentStartup = true,
            CloseToTray = false,
        });

        var restored = store.Load();

        Assert.False(restored.LaunchAtStartup);
        Assert.False(restored.SilentStartup);
        Assert.False(restored.CloseToTray);
        Assert.True(File.Exists(root.DesktopSettingsPath));
        Assert.StartsWith(LocalEncryptedFileStore.EnvelopePrefix, File.ReadAllText(root.DesktopSettingsPath));
    }

    [Fact]
    public void DesktopRuntimeStore_PersistsAndClearsRuntimeInstanceUnderDotNetRoot()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var store = root.DesktopRuntime();

        var savedPath = store.Save(port: 51234, processId: 1234);
        var restored = store.Load();

        Assert.Equal(".runtime/data/.desktop/runtime_instance.json", savedPath.Replace('\\', '/'));
        Assert.Equal(1234, restored.ProcessId);
        Assert.Equal(51234, restored.Port);
        Assert.True(restored.HasEndpoint);
        Assert.True(File.Exists(store.RuntimeInstancePath));
        Assert.StartsWith(LocalEncryptedFileStore.EnvelopePrefix, File.ReadAllText(store.RuntimeInstancePath));

        store.Clear(expectedProcessId: 9999);
        Assert.True(File.Exists(store.RuntimeInstancePath));

        store.Clear(expectedProcessId: 1234);
        Assert.False(File.Exists(store.RuntimeInstancePath));
        Assert.False(store.Load().HasEndpoint);
    }

    [Fact]
    public void ScheduleStore_PersistsNormalizedAndRawPayloadsUnderDotNetRuntimeRoot()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var studentStore = root.ForStudent("2026/0001");
        var scheduleStore = studentStore.Schedules();
        var payload = new Dictionary<string, object?>
        {
            ["source"] = "移动教务课表",
            ["source_label"] = "移动教务课表",
            ["source_key"] = "mobile_schedule",
            ["source_type"] = "mobile",
            ["week_label"] = "第7周",
            ["current_week_label"] = "第7周",
            ["updated_at"] = "2026-04-23T12:00:00Z",
            ["term"] = new Dictionary<string, object?>
            {
                ["academic_term"] = "2025-2026-2",
                ["label"] = "2025-2026-2",
            },
            ["week_dates"] = new Dictionary<string, object?>
            {
                ["星期一"] = "2026-04-20",
                ["星期日"] = "2026-04-26",
            },
            ["rows"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["course_name"] = "高等数学",
                    ["weekday"] = "星期一",
                    ["section"] = "第1-2节",
                    ["time_range"] = "08:00-09:40",
                    ["teacher"] = "张老师",
                    ["room"] = "1-101,东楼",
                },
            },
        };
        var raw = new Dictionary<string, object?>
        {
            ["code"] = "1",
            ["data"] = new List<object?>(),
        };

        var savedFiles = scheduleStore.SaveSourceSchedule(payload, raw, "mobile_schedule", "第7周");
        var restoredByWeek = scheduleStore.LoadScheduleForWeek("7", ["mobile"]);
        var restoredLatest = scheduleStore.LoadLatestSourceSchedule(["mobile"]);
        var inferredWeek = scheduleStore.FindScheduleWeekLabelForDate(new DateOnly(2026, 4, 22), ["mobile"]);

        Assert.Equal("2026_0001", studentStore.StudentId);
        Assert.Equal("第7周", restoredByWeek!.GetValueOrDefault("week_label"));
        Assert.Equal("第7周", restoredLatest!.GetValueOrDefault("week_label"));
        Assert.Equal("第7周", inferredWeek);
        Assert.Contains(savedFiles, path => path.EndsWith("mobile_schedule.json", StringComparison.Ordinal));
        Assert.Contains(savedFiles, path => path.EndsWith("mobile_schedule_raw.json", StringComparison.Ordinal));
        var csvRelativePath = Assert.Single(savedFiles, path => path.EndsWith("mobile_schedule.csv", StringComparison.Ordinal));
        var csvPath = Path.Combine(workspace.RootPath, csvRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var csv = File.ReadAllText(csvPath);
        Assert.Contains("week_label,weekday,section,time_text,time_range,block_time,small_section_1,small_section_2,break_minutes,course_name,teacher,room,weeks,credit", csv);
        Assert.Contains("第7周,星期一,第1-2节,,08:00-09:40,,,,,高等数学,张老师,\"1-101,东楼\",,", csv);
        Assert.DoesNotContain("source_key", csv);
        Assert.DoesNotContain("source_type", csv);
        Assert.All(savedFiles, path => Assert.StartsWith(".runtime/data/students/2026_0001/schedules/", path.Replace('\\', '/')));
        Assert.True(Directory.Exists(Path.Combine(root.RootPath, "students", "2026_0001", "schedules")));
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "data", "students")));
    }

    [Fact]
    public void ScheduleStore_WritesCsvForTraditionalJwSchedule()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var studentStore = root.ForStudent("20260001");
        var scheduleStore = studentStore.Schedules();
        var payload = new Dictionary<string, object?>
        {
            ["source_key"] = "jw_schedule",
            ["source_type"] = "jw",
            ["week_label"] = "第8周",
            ["rows"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["course_name"] = "高等数学",
                    ["weekday"] = "星期一",
                    ["section"] = "第1-2节",
                    ["time_range"] = "08:00-09:40",
                    ["teacher"] = "张老师",
                    ["room"] = "1-101",
                },
            },
        };

        var savedFiles = scheduleStore.SaveSourceSchedule(payload, [], "jw_schedule", "第8周");

        Assert.Contains(savedFiles, path => path.EndsWith("jw_schedule.json", StringComparison.Ordinal));
        var csvRelativePath = Assert.Single(savedFiles, path => path.EndsWith("jw_schedule.csv", StringComparison.Ordinal));
        var csvPath = Path.Combine(workspace.RootPath, csvRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var csv = File.ReadAllText(csvPath);
        Assert.Contains("week_label,weekday,section,time_text,time_range,block_time,small_section_1,small_section_2,break_minutes,course_name,teacher,room,weeks,credit", csv);
        Assert.Contains("第8周,星期一,第1-2节,,08:00-09:40,,,,,高等数学,张老师,1-101,,", csv);
        Assert.DoesNotContain("source_key", csv);
        Assert.DoesNotContain("source_type", csv);
    }

    [Fact]
    public void ScheduleStore_LoadScheduleForWeekMatchesChineseAndArabicWeekLabels()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var studentStore = root.ForStudent("20260001");
        var scheduleStore = studentStore.Schedules();
        var payload = new Dictionary<string, object?>
        {
            ["source_key"] = "jw_schedule",
            ["source_type"] = "jw",
            ["week_label"] = "第八周",
            ["rows"] = new List<object?>(),
        };

        var savedFiles = scheduleStore.SaveSourceSchedule(payload, [], "jw_schedule", "第八周");

        Assert.NotNull(scheduleStore.LoadScheduleForWeek("第八周", ["jw_schedule"]));
        Assert.NotNull(scheduleStore.LoadScheduleForWeek("第8周", ["jw_schedule"]));
        Assert.NotNull(scheduleStore.LoadScheduleForWeek("8", ["jw_schedule"]));
        Assert.Contains(savedFiles, path => path.Replace('\\', '/').Contains("/第8周/", StringComparison.Ordinal));
        Assert.DoesNotContain(savedFiles, path => path.Replace('\\', '/').Contains("/第八周/", StringComparison.Ordinal));
    }

    [Fact]
    public void ScheduleStore_DefaultDateProviderUsesLocalDesktopDate()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var studentStore = root.ForStudent("20260001");
        var scheduleStore = new XkeScheduleStore(root, studentStore, () => new DateOnly(2026, 4, 27));
        var payload = new Dictionary<string, object?>
        {
            ["source"] = "移动教务课表",
            ["source_label"] = "移动教务课表",
            ["source_key"] = "mobile_schedule",
            ["source_type"] = "mobile",
            ["week_label"] = "第8周",
            ["updated_at"] = "2026-04-27T08:00:00+08:00",
            ["week_dates"] = new Dictionary<string, object?>
            {
                ["星期一"] = "2026-04-27",
                ["星期日"] = "2026-05-03",
            },
            ["rows"] = new List<object?> { new Dictionary<string, object?> { ["course_name"] = "高等数学" } },
        };

        scheduleStore.SaveSourceSchedule(payload, [], "mobile_schedule", "第8周");

        Assert.Equal("第8周", scheduleStore.FindScheduleWeekLabelForDate(null, ["mobile"]));
    }

    [Fact]
    public void DotNetDataRootFactory_IgnoresLegacyDataEnvironmentVariables()
    {
        using var workspace = new TemporaryDirectory();
        using var legacyRoot = new TemporaryDirectory();
        var previousLegacy = Environment.GetEnvironmentVariable("XKE_DATA_DIR");
        var previousPortable = Environment.GetEnvironmentVariable("XKE_PORTABLE_DATA_DIR");
        var previousDotNet = Environment.GetEnvironmentVariable("XKE_DOTNET_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XKE_DATA_DIR", legacyRoot.RootPath);
            Environment.SetEnvironmentVariable("XKE_PORTABLE_DATA_DIR", Path.Combine(legacyRoot.RootPath, "portable"));
            Environment.SetEnvironmentVariable("XKE_DOTNET_DATA_DIR", null);

            var root = XkeDataRootFactory.CreateDefault(workspace.RootPath);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            Assert.Equal(Path.Combine(localAppData, ".XKE", "data"), root.RootPath);
            Assert.False(root.RootPath.StartsWith(legacyRoot.RootPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XKE_DATA_DIR", previousLegacy);
            Environment.SetEnvironmentVariable("XKE_PORTABLE_DATA_DIR", previousPortable);
            Environment.SetEnvironmentVariable("XKE_DOTNET_DATA_DIR", previousDotNet);
        }
    }

    [Fact]
    public void NetworkTelemetryAndFeedbackStores_StayInsideDotNetRuntimeRoot()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var studentStore = root.ForStudent("20260001");

        var credentialPath = studentStore.NetworkCredentials().Save(new StoredNetworkCredentialDocument
        {
            Provider = "telecom",
            Account = "telecom-user",
            Password = "secret-pass",
            UpdatedAt = "2026-04-23T12:00:00Z",
        });
        var restoredCredential = studentStore.NetworkCredentials().Load("telecom");

        var installation = root.Telemetry().LoadOrCreateInstallationProfile(() => "2026-04-23T12:00:00Z");
        var keyProfile = root.Telemetry().LoadOrCreateKeyProfile(() => "2026-04-23T12:00:00Z");
        var spool = root.Telemetry().EnsureSpoolDirectory();

        var runtimePath = studentStore.SaveAppRuntimeInfo(new Dictionary<string, object?>
        {
            ["usage"] = new Dictionary<string, object?>
            {
                ["total_runtime_seconds"] = 30,
            },
        });
        var snapshotPath = studentStore.SaveRemoteSensingSnapshot(new Dictionary<string, object?>
        {
            ["reported_at"] = "2026-04-23T12:01:00Z",
        });
        var historyPath = studentStore.SaveNetworkAuthHistory(new NetworkAuthHistoryDocument
        {
            Records =
            [
                new NetworkAuthRecordDocument
                {
                    RecordedAt = "2026-04-23T12:02:00Z",
                    Provider = "campus",
                    ProviderLabel = "校园账号",
                    Account = "20******01",
                    Success = true,
                    Message = "ok",
                },
            ],
        });
        var feedbackKey = root.FeedbackOutbox().Save("20260001", "fb-1", new Dictionary<string, object?>
        {
            ["feedback_id"] = "fb-1",
            ["content"] = "need help",
        });

        Assert.NotNull(restoredCredential);
        Assert.Equal("telecom-user", restoredCredential!.Account);
        Assert.Equal("secret-pass", restoredCredential.Password);
        Assert.DoesNotContain("secret-pass", File.ReadAllText(Path.Combine(root.RootPath, "students", "20260001", "info", "network", "telecom.json.dpapi")));

        Assert.Equal(".runtime/data/students/20260001/info/network/telecom.json.dpapi", credentialPath.Replace('\\', '/'));
        Assert.Equal(".runtime/data/.cloud/telemetry/spool", spool.Replace('\\', '/'));
        Assert.Equal(".runtime/data/students/20260001/info/app_runtime.json", runtimePath.Replace('\\', '/'));
        Assert.Equal(".runtime/data/students/20260001/info/remote_sensing_snapshot.json", snapshotPath.Replace('\\', '/'));
        Assert.Equal(".runtime/data/students/20260001/info/network_auth_history.json", historyPath.Replace('\\', '/'));
        Assert.Equal(".runtime/data/.feedback/outbox/20260001/fb-1.json", feedbackKey.Replace('\\', '/'));
        Assert.Equal("2026-04-23T12:00:00Z", installation["first_used_at"]);
        Assert.Equal(LocalEncryptedFileStore.KeyId, keyProfile.SourcePath);
        Assert.False(File.Exists(root.TelemetryKeyPath));

        AssertEncryptedFileDoesNotContain(root.TelemetryInstallationPath, "install_uuid");
        AssertEncryptedFileDoesNotContain(Path.Combine(workspace.RootPath, runtimePath.Replace('/', Path.DirectorySeparatorChar)), "total_runtime_seconds");
        AssertEncryptedFileDoesNotContain(Path.Combine(workspace.RootPath, snapshotPath.Replace('/', Path.DirectorySeparatorChar)), "reported_at");
        AssertEncryptedFileDoesNotContain(Path.Combine(workspace.RootPath, historyPath.Replace('/', Path.DirectorySeparatorChar)), "校园账号");
        AssertEncryptedFileDoesNotContain(Path.Combine(workspace.RootPath, feedbackKey.Replace('/', Path.DirectorySeparatorChar)), "need help");

        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "data", ".telemetry")));
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "data", ".feedback")));
    }

    [Fact]
    public void CourseStore_PersistsNormalizedAndRawPayloadsInsideDotNetRuntimeRoot()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var studentStore = root.ForStudent("2026/0001");
        var courseStore = studentStore.Course();
        var rounds = new CourseRoundsPayload(
            Rounds:
            [
                new CourseRound(
                    RoundId: "round/1",
                    Name: "公共选修课第一轮",
                    Term: "2025-2026-2",
                    TimeRange: "2026-04-23 ~ 2026-04-25",
                    Status: "open"),
            ],
            Count: 1,
            Cached: false,
            UpdatedAt: "2026-04-23T12:00:00Z",
            Warnings: ["fixture-source"]);
        var preview = new CoursePreviewPayload(
            RoundId: "round/1",
            Courses:
            [
                new CourseItem
                {
                    Kch = "A1001",
                    Kcmc = "大学美育",
                    Xf = "2",
                    Skls = "李老师",
                    Sksj = "周一 1-2 节",
                    Skdd = "教学楼 101",
                    Xqmc = "主校区",
                    Xkrs = "30",
                    Syrs = "12",
                    Ctsm = string.Empty,
                    Szkcflmc = "公共选修",
                    Jx02Id = "course-1001",
                    Jx0404Id = "section-2002",
                },
            ],
            Count: 1,
            Cached: false,
            UpdatedAt: "2026-04-23T12:02:00Z",
            Warnings: []);

        var roundsPath = courseStore.SaveRounds(rounds, new Dictionary<string, object?> { ["source"] = "fake" });
        var previewPath = courseStore.SavePreview(preview, new Dictionary<string, object?> { ["source"] = "fake-preview" });
        var restoredRounds = courseStore.LoadRounds();
        var restoredPreview = courseStore.LoadPreview("round/1");

        Assert.Equal("2026_0001", studentStore.StudentId);
        Assert.Equal(".runtime/data/students/2026_0001/course/rounds.json", roundsPath.Replace('\\', '/'));
        Assert.Equal(".runtime/data/students/2026_0001/course/previews/round_1/preview.json", previewPath.Replace('\\', '/'));
        Assert.Equal("公共选修课第一轮", restoredRounds!.Rounds[0].Name);
        Assert.Equal("大学美育", restoredPreview!.Courses[0].Kcmc);
        Assert.True(File.Exists(Path.Combine(root.RootPath, "students", "2026_0001", "course", "rounds_raw.json")));
        Assert.True(File.Exists(Path.Combine(root.RootPath, "students", "2026_0001", "course", "previews", "round_1", "preview_raw.json")));
        AssertEncryptedFileDoesNotContain(Path.Combine(workspace.RootPath, roundsPath.Replace('/', Path.DirectorySeparatorChar)), "公共选修课第一轮");
        AssertEncryptedFileDoesNotContain(Path.Combine(workspace.RootPath, previewPath.Replace('/', Path.DirectorySeparatorChar)), "大学美育");
        AssertEncryptedFileDoesNotContain(Path.Combine(root.RootPath, "students", "2026_0001", "course", "rounds_raw.json"), "fake");
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "data", "students", "2026_0001", "course")));
    }

    private static XkeDataRoot CreateTemporaryDataRoot(TemporaryDirectory workspace)
    {
        return XkeDataRootFactory.CreateDefault(
            workspace.RootPath,
            workspace.RootPath,
            Path.Combine(workspace.RootPath, ".runtime", "data"));
    }

    private static void AssertEncryptedFileDoesNotContain(string path, string plaintext)
    {
        var content = File.ReadAllText(path);
        Assert.StartsWith(LocalEncryptedFileStore.EnvelopePrefix, content);
        Assert.DoesNotContain(plaintext, content, StringComparison.Ordinal);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"xke-auth-store-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
