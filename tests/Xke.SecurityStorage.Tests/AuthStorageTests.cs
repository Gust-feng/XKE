using System.Text.Json;
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
    public void LocalEncryptedFileStore_RoundTripsWithDpapiEnvelope()
    {
        using var workspace = new TemporaryDirectory();
        var filePath = Path.Combine(workspace.RootPath, "local.json");
        var store = new LocalEncryptedFileStore();

        store.WriteJson(filePath, new Dictionary<string, string>
        {
            ["value"] = "secret-value",
        });

        var restored = store.ReadJson<Dictionary<string, string>>(filePath);
        var content = File.ReadAllText(filePath);

        Assert.NotNull(restored);
        Assert.Equal("secret-value", restored!["value"]);
        Assert.StartsWith(LocalEncryptedFileStore.EnvelopePrefix, content);
        Assert.DoesNotContain("secret-value", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalEncryptedFileStore_RejectsLegacyAesEnvelope()
    {
        using var workspace = new TemporaryDirectory();
        var filePath = Path.Combine(workspace.RootPath, "legacy.json");
        var store = new LocalEncryptedFileStore();

        File.WriteAllText(filePath, "XKEENC1." + Convert.ToBase64String(new byte[32]));

        Assert.Throws<InvalidDataException>(() => store.ReadText(filePath));
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
        Assert.Equal("测试同学", JsonDocument.Parse(File.ReadAllText(studentStore.ProfilePath)).RootElement.GetProperty("name").GetString());
        Assert.Equal("forest-notes", JsonDocument.Parse(File.ReadAllText(studentStore.PreferencesPath)).RootElement.GetProperty("appearance").GetProperty("theme").GetString());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(studentStore.GetAvatarPath()!));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, studentStore.ReadAvatar(studentStore.GetAvatarPath()!));

        sessionStore.Remove(session.SessionId);
        Assert.Null(sessionStore.Load(session.SessionId));
    }

    [Fact]
    public void StudentBusinessFiles_RejectEncryptedContentsWithoutDecryptingOrChangingFiles()
    {
        using var workspace = new TemporaryDirectory();
        var student = CreateTemporaryDataRoot(workspace).ForStudent("20260001");
        var encrypted = new LocalEncryptedFileStore();
        encrypted.WriteJson(student.ProfilePath, new StudentProfileDocument { StudentId = "20260001", Name = "旧测试资料" });
        encrypted.WriteJson(student.PreferencesPath, new AppSettingsDocument());
        var profileBytes = File.ReadAllBytes(student.ProfilePath);
        var preferencesBytes = File.ReadAllBytes(student.PreferencesPath);

        Assert.Null(student.LoadProfile());
        Assert.Null(student.LoadPreferences());
        Assert.Equal(profileBytes, File.ReadAllBytes(student.ProfilePath));
        Assert.Equal(preferencesBytes, File.ReadAllBytes(student.PreferencesPath));
    }

    [Fact]
    public void JwSessionStore_ProtectsAndRemovesStoredCookies()
    {
        using var workspace = new TemporaryDirectory();
        var root = CreateTemporaryDataRoot(workspace);
        var sessions = root.JwSessions();

        sessions.Save(new JwSessionDocument
        {
            User = "20260001",
            UpdatedAt = "2026-09-08T00:00:00Z",
            Cookies =
            [
                new JwSessionCookieDocument { Name = "JSESSIONID", Value = "secret-session", Host = "222.243.161.213", Path = "/" },
                new JwSessionCookieDocument { Name = "SERVERID", Value = "secret-server", Host = "222.243.161.213", Path = "/" },
            ],
        });

        var restored = sessions.Load("20260001");
        var protectedPath = Path.Combine(root.JwSessionsDirectory, "20260001.json.dpapi");

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Cookies.Count);
        Assert.DoesNotContain("secret-session", File.ReadAllText(protectedPath));
        Assert.DoesNotContain("secret-server", File.ReadAllText(protectedPath));

        sessions.Remove("20260001");
        Assert.Null(sessions.Load("20260001"));
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
        Assert.Contains("student_id,academic_term,week_label,weekday,course_name,teacher,room,section_numbers,week_numbers,time_segments,weeks,credit,hours_text", csv);
        Assert.Contains("第7周,星期一,高等数学,张老师,\"1-101,东楼\"", csv);
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
        Assert.Contains("student_id,academic_term,week_label,weekday,course_name,teacher,room,section_numbers,week_numbers,time_segments,weeks,credit,hours_text", csv);
        Assert.Contains("第8周,星期一,高等数学,张老师,1-101", csv);
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
