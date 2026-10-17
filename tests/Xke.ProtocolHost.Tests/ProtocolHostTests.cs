using System.Net;
using System.Text.Json;
using Xke.Core.Schedule;
using Xke.ProtocolHost;
using Xunit;

namespace Xke.ProtocolHost.Tests;

public sealed class ProtocolHostTests
{
    [Fact]
    public async Task ApiKey_IsRequiredBeforeSchoolCalls_HealthIsPublic()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Remove(XkeProtocolHostApplication.ApiKeyHeader);
        using var health = await host.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var missing = await host.LoginAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.False((await ProtocolTestHost.ReadAsync(missing)).GetProperty("ok").GetBoolean());
        host.Client.DefaultRequestHeaders.Add(XkeProtocolHostApplication.ApiKeyHeader, "incorrect-key");
        using var wrong = await host.Client.GetAsync("/api/auth/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Empty(host.School.Requests);
    }

    [Theory]
    [InlineData("/api/auth/session")]
    [InlineData("/api/auth/profile")]
    [InlineData("/api/schedule/week")]
    [InlineData("/api/schedule/semester")]
    public async Task UnconnectedRequests_Return401WithoutSchoolCalls(string route)
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var response = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(host.School.Requests);
    }

    [Fact]
    public async Task Login_RecognizesCaptcha_UsesServerIdentity_StoresOnlyPlainProfile()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var response = await host.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ProtocolTestHost.ReadAsync(response);
        var data = json.GetProperty("data");
        Assert.True(data.GetProperty("connected").GetBoolean());
        Assert.False(data.TryGetProperty("session_id", out _));
        var student = data.GetProperty("student");
        Assert.Equal("SERVER001", student.GetProperty("student_id").GetString());
        Assert.Equal(4, student.GetProperty("program_duration_years").GetInt32());
        Assert.Equal("2024-09-01", student.GetProperty("enrollment_date").GetString());
        Assert.Equal(1, host.Captcha.Calls);
        Assert.Equal(1, host.School.LoginSubmissions);
        var files = Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories);
        var profile = Assert.Single(files);
        Assert.EndsWith(Path.Combine("students", "SERVER001", "profile.json"), profile);
        using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(profile));
        Assert.Equal("Test Student", stored.RootElement.GetProperty("name").GetString());
        Assert.DoesNotContain("fake-school-password", await File.ReadAllTextAsync(profile));
        Assert.DoesNotContain("fake-school-cookie", await File.ReadAllTextAsync(profile));
        var calls = host.School.Requests.Count;
        using var session = await host.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(calls, host.School.Requests.Count);
        using var refreshed = await host.Client.GetAsync("/api/auth/profile");
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Single(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task LoginFailures_PreserveExistingConnection_AndDoNotRetry()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        var profile = await File.ReadAllTextAsync(Path.Combine(host.Root, "students", "SERVER001", "profile.json"));
        host.School.RejectLogin = true;
        using var rejected = await host.LoginAsync(password: "fake-wrong-password");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(2, host.School.LoginSubmissions);
        using var session = await host.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(profile, await File.ReadAllTextAsync(Path.Combine(host.Root, "students", "SERVER001", "profile.json")));
    }

    [Fact]
    public async Task OcrFailure_DoesNotSubmitLoginOrWriteBusinessFiles()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        host.Captcha.Failure = new JwScheduleException("fake OCR failure", 502);
        using var response = await host.LoginAsync();
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, host.School.LoginSubmissions);
        Assert.False(Directory.Exists(host.Root));
    }

    [Fact]
    public async Task WeekAndSemester_UseExistingModelsAndStorage_WithReadOnlySchoolRequests()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        host.School.Requests.Clear();
        using var response = await host.Client.GetAsync("/api/schedule/week?week=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var week = (await ProtocolTestHost.ReadAsync(response)).GetProperty("data");
        Assert.Equal("第2周", week.GetProperty("week_label").GetString());
        Assert.Equal(1, week.GetProperty("current_week_number").GetInt32());
        Assert.Equal("2026-09-14", week.GetProperty("week_dates").GetProperty("星期一").GetString());
        Assert.Equal("2026-2027-1", week.GetProperty("term").GetProperty("academic_term").GetString());
        Assert.Equal("第1周", week.GetProperty("available_weeks")[0].GetProperty("label").GetString());
        Assert.False(week.GetProperty("cached").GetBoolean());
        var row = Assert.Single(week.GetProperty("rows").EnumerateArray());
        Assert.Equal(4, row.GetProperty("section_numbers").GetArrayLength());
        Assert.Equal(16, row.GetProperty("week_numbers").GetArrayLength());
        Assert.Equal(2, row.GetProperty("time_segments").GetArrayLength());
        Assert.Equal(1, week.GetProperty("notes").GetArrayLength());
        var savedWeek = week.GetProperty("saved_files").EnumerateArray().Select(item => Path.Combine(host.Root, item.GetString()!)).ToArray();
        Assert.Equal(2, savedWeek.Length);
        Assert.All(savedWeek, path => Assert.True(File.Exists(path)));
        var csv = await File.ReadAllTextAsync(savedWeek.Single(path => path.EndsWith(".csv", StringComparison.Ordinal)));
        Assert.Contains("SERVER001,2026-2027-1,第2周", csv);
        Assert.Contains("1;2;3;4", csv);
        Assert.Contains("08:00-09:40;10:00-11:40", csv);
        using var semesterResponse = await host.Client.GetAsync("/api/schedule/semester?academic_term=2025-2026-2");
        Assert.Equal(HttpStatusCode.OK, semesterResponse.StatusCode);
        var semester = (await ProtocolTestHost.ReadAsync(semesterResponse)).GetProperty("data");
        Assert.Equal("2025-2026-2", semester.GetProperty("term").GetProperty("academic_term").GetString());
        Assert.Equal(2, semester.GetProperty("available_terms").GetArrayLength());
        Assert.All(semester.GetProperty("saved_files").EnumerateArray(), item =>
        {
            Assert.Contains("/semester/semester_schedule.", item.GetString());
            Assert.True(File.Exists(Path.Combine(host.Root, item.GetString()!)));
        });
        Assert.All(host.School.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.False(Directory.Exists(Path.Combine(host.Root, "auth")));
        Assert.False(Directory.Exists(Path.Combine(host.Root, ".desktop")));
    }

    [Fact]
    public async Task ValidEmptyWeek_Succeeds_AndWritesHeaderOnlyCsv()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        host.School.EmptyWeek = true;
        using var response = await host.Client.GetAsync("/api/schedule/week");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await ProtocolTestHost.ReadAsync(response)).GetProperty("data");
        Assert.Equal(0, data.GetProperty("rows").GetArrayLength());
        var csv = data.GetProperty("saved_files").EnumerateArray().Select(item => item.GetString()!).Single(path => path.EndsWith(".csv", StringComparison.Ordinal));
        Assert.Single(await File.ReadAllLinesAsync(Path.Combine(host.Root, csv)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiration_Returns401_ClearsMemory_AndPreservesPreviousSnapshot(bool http401)
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        using var first = await host.Client.GetAsync("/api/schedule/week");
        var data = (await ProtocolTestHost.ReadAsync(first)).GetProperty("data");
        var path = Path.Combine(host.Root, data.GetProperty("saved_files")[0].GetString()!);
        var original = await File.ReadAllTextAsync(path);
        host.School.Expired = true;
        host.School.Http401OnExpiration = http401;
        using var expired = await host.Client.GetAsync("/api/schedule/week");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var calls = host.School.Requests.Count;
        using var session = await host.Client.GetAsync("/api/auth/session");
        using var again = await host.Client.GetAsync("/api/schedule/semester");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
        Assert.Equal(calls, host.School.Requests.Count);
        Assert.Equal(1, host.School.LoginSubmissions);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Logout_LeavesBusinessFiles_AndRestartDoesNotRestoreSecrets()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        using var logout = await host.Client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var disconnected = await host.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, disconnected.StatusCode);
        Assert.True(File.Exists(Path.Combine(host.Root, "students", "SERVER001", "profile.json")));
        await using var restarted = await ProtocolTestHost.StartAsync(host.Root);
        using var restored = await restarted.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, restored.StatusCode);
        Assert.Empty(restarted.School.Requests);
    }

    [Fact]
    public async Task SourceFailure_DoesNotOverwriteSnapshotsOrDisconnect()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        using var first = await host.Client.GetAsync("/api/schedule/semester");
        var data = (await ProtocolTestHost.ReadAsync(first)).GetProperty("data");
        var path = Path.Combine(host.Root, data.GetProperty("saved_files")[0].GetString()!);
        var original = await File.ReadAllTextAsync(path);
        host.School.FailSchedule = true;
        using var failed = await host.Client.GetAsync("/api/schedule/semester");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
        using var session = await host.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task ConcurrentProtectedRequests_SerializeOneSchoolConnection()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        host.School.ProtectedDelay = TimeSpan.FromMilliseconds(20);
        var responses = await Task.WhenAll(host.Client.GetAsync("/api/schedule/week"),
            host.Client.GetAsync("/api/schedule/semester"), host.Client.GetAsync("/api/auth/profile"));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(1, host.School.MaxConcurrentProtectedRequests);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("/api/schedule/week?week=not-a-number")]
    [InlineData("/api/schedule/week?week=2147483648")]
    public async Task MalformedWeek_ReturnsEnvelopeWithoutSchoolCalls(string route)
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var response = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await ProtocolTestHost.ReadAsync(response);
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").ValueKind);
        Assert.Empty(host.School.Requests);
    }

    [Fact]
    public async Task MalformedLogin_ReturnsSafeEnvelopeWithoutSchoolCalls()
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var content = new StringContent("{\"password\":\"fake-private-value\"", System.Text.Encoding.UTF8, "application/json");
        using var response = await host.Client.PostAsync("/api/auth/login", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await ProtocolTestHost.ReadAsync(response);
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.DoesNotContain("fake-private-value", json.GetRawText());
        Assert.Empty(host.School.Requests);
    }

    [Theory]
    [InlineData("/api/schedule/week?week=0", HttpStatusCode.BadRequest)]
    [InlineData("/api/schedule/week?week=99", HttpStatusCode.NotFound)]
    [InlineData("/api/schedule/week?academic_term=2025-2026-2", HttpStatusCode.BadRequest)]
    [InlineData("/api/schedule/semester?academic_term=unknown", HttpStatusCode.NotFound)]
    public async Task UnsupportedContext_DoesNotSaveAnotherWeek(string route, HttpStatusCode expected)
    {
        await using var host = await ProtocolTestHost.StartAsync();
        using var login = await host.LoginAsync();
        using var response = await host.Client.GetAsync(route);
        Assert.Equal(expected, response.StatusCode);
        Assert.Single(Directory.GetFiles(host.Root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("", false, "http://127.0.0.1:8866/recognize")]
    [InlineData("key", true, "http://127.0.0.1:8866/recognize")]
    [InlineData("key", false, "file:///recognize")]
    public void InvalidConfiguration_IsRejectedBeforeStartup(string key, bool relativeRoot, string endpoint)
    {
        Assert.Throws<InvalidOperationException>(() => XkeProtocolHostApplication.Build(new ProtocolHostOptions
        {
            ApiKey = key, DataRootPath = relativeRoot ? "relative-dir" : Path.GetTempPath(), DdddOcrEndpoint = new Uri(endpoint),
        }));
    }
}
