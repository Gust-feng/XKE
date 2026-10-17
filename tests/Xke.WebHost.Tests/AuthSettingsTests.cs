using System.Net.Http.Json;
using System.Text.Json;
using Xke.Storage;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class AuthSettingsTests
{
    [Fact]
    public void Apply_PreservesCustomThemeAndPositiveIntervals()
    {
        var settings = AuthSettingsCatalog.Apply(AuthSettingsCatalog.CreateDefault(), new PreferencesRequest
        {
            Theme = "personal-dark",
            Schedule = new ScheduleSettingsRequest
            {
                AutoRefreshIntervalHours = 7,
                LessonReminderMinutes = 12,
                AutoRefreshDailyTime = "7:05",
            },
            Network = new NetworkSettingsRequest
            {
                HeartbeatIntervalMinutes = 9, PortalUrl = "http://10.20.0.1/",
                ConnectivityCheckUrls = ["https://example.test/check", "file:///not-http", "https://example.test/check"],
            },
        });
        Assert.Equal("personal-dark", settings.Appearance.Theme);
        Assert.Equal(7, settings.Schedule.AutoRefreshIntervalHours);
        Assert.Equal(15, settings.Schedule.LessonReminderMinutes);
        Assert.Equal("07:05", settings.Schedule.AutoRefreshDailyTime);
        Assert.Equal(9, settings.Network.HeartbeatIntervalMinutes);
        Assert.Equal("http://10.20.0.1/", settings.Network.PortalUrl);
        Assert.Equal(new[] { "https://example.test/check" }, settings.Network.ConnectivityCheckUrls);
        Assert.Equal(9, AuthSettingsCatalog.Normalize(settings).Network.HeartbeatIntervalMinutes);
    }

    [Fact]
    public void Apply_PartialPatchRetainsContextAndExplicitEmptyClearsOnlyFreeText()
    {
        var current = AuthSettingsCatalog.Apply(AuthSettingsCatalog.CreateDefault(), new PreferencesRequest
        {
            Theme = "custom",
            Schedule = new ScheduleSettingsRequest
            {
                Source = "cookie", WeekMode = "manual", ManualWeek = "8", CookieRq = "2026-10-07",
                AcademicTerm = "2026-2027-1", TermLabel = "秋季", MockDay = "monday", MockTime = "09:00",
                AutoRefreshMode = "interval", AutoRefreshDailyTime = "21:30", AutoRefreshIntervalHours = 9,
                LessonReminderMinutes = 18, WeekendAutoSwitchEnabled = true,
                IgnoredWeekendBlocks = new() { ["saturday"] = [1, 2], ["sunday"] = [3] },
            },
            Network = new NetworkSettingsRequest
            {
                Provider = "telecom", HeartbeatIntervalMinutes = 11,
                PortalUrl = "https://portal.example.test/", ConnectivityCheckUrls = ["https://check.example.test/"],
            },
            Ui = new UISettingsRequest { ShowWeekend = false, InactiveCourseMode = "dim", Panels = new() { Schedule = true, Appearance = true } },
        });
        var updated = AuthSettingsCatalog.Apply(current, new PreferencesRequest
        {
            Schedule = new() { AutoRefreshEnabled = true, LessonReminderEnabled = true },
            Network = new() { HeartbeatEnabled = true },
            Ui = new() { Panels = new() { LoginMode = true } },
            Auth = new(), Appearance = new(),
        });
        Assert.Equal(JsonSerializer.Serialize(current.Schedule), JsonSerializer.Serialize(AuthSettingsCatalog.Apply(updated,
            new PreferencesRequest { Schedule = new() { AutoRefreshEnabled = false, LessonReminderEnabled = false } }).Schedule));
        Assert.Equal(current.Appearance.Theme, updated.Appearance.Theme);
        Assert.Equal(current.Auth.PreloginMode, updated.Auth.PreloginMode);
        Assert.Equal(current.Network.Provider, updated.Network.Provider);
        Assert.Equal(current.Network.PortalUrl, updated.Network.PortalUrl);
        Assert.Equal(current.Network.ConnectivityCheckUrls, updated.Network.ConnectivityCheckUrls);
        Assert.Equal(11, updated.Network.HeartbeatIntervalMinutes);
        Assert.False(updated.Ui.ShowWeekend);
        Assert.Equal("dim", updated.Ui.InactiveCourseMode);
        Assert.True(updated.Ui.Panels.Schedule && updated.Ui.Panels.Appearance && updated.Ui.Panels.LoginMode);
        Assert.Equal(JsonSerializer.Serialize(updated), JsonSerializer.Serialize(AuthSettingsCatalog.Apply(updated, new PreferencesRequest
        { Schedule = new(), Network = new(), Appearance = new(), Auth = new(), Ui = new() })));

        var nullPatch = JsonSerializer.Deserialize<PreferencesRequest>("""
            {"theme":null,"appearance":{"theme":null},"auth":{"prelogin_mode":null},
             "schedule":{"manual_week":null,"academic_term":null,"cookie_rq":null,"term_label":null,"mock_day":null,"mock_time":null,
                         "ignored_weekend_blocks":{"saturday":null}},
             "network":{"portal_url":null,"connectivity_check_urls":null},
             "ui":{"show_weekend":null,"inactive_course_mode":null,"panels":{"schedule":null}}}
            """)!;
        Assert.Equal(JsonSerializer.Serialize(updated), JsonSerializer.Serialize(AuthSettingsCatalog.Apply(updated, nullPatch)));

        var cleared = AuthSettingsCatalog.Apply(updated, new PreferencesRequest
        {
            Schedule = new() { ManualWeek = "", AcademicTerm = " ", CookieRq = "", TermLabel = "", MockDay = "", MockTime = "",
                WeekMode = "", AutoRefreshDailyTime = "", IgnoredWeekendBlocks = new() { ["saturday"] = [] } },
            Network = new() { PortalUrl = "", ConnectivityCheckUrls = [] },
        });
        Assert.Empty(cleared.Schedule.ManualWeek);
        Assert.Empty(cleared.Schedule.AcademicTerm);
        Assert.Empty(cleared.Schedule.CookieRq);
        Assert.Empty(cleared.Schedule.TermLabel);
        Assert.Empty(cleared.Schedule.MockDay);
        Assert.Empty(cleared.Schedule.MockTime);
        Assert.Equal(updated.Schedule.WeekMode, cleared.Schedule.WeekMode);
        Assert.Equal(updated.Schedule.AutoRefreshDailyTime, cleared.Schedule.AutoRefreshDailyTime);
        Assert.Empty(cleared.Schedule.IgnoredWeekendBlocks.Saturday);
        Assert.Equal(new[] { 3 }, cleared.Schedule.IgnoredWeekendBlocks.Sunday);
        Assert.Equal(updated.Network.PortalUrl, cleared.Network.PortalUrl);
        Assert.Equal(updated.Network.ConnectivityCheckUrls, cleared.Network.ConnectivityCheckUrls);
    }

    [Theory]
    [InlineData("muted")]
    [InlineData("hidden")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Apply_InvalidUiModeRetainsExistingAndNormalizeUsesDefault(string? mode)
    {
        var current = new AppSettingsDocument { Ui = new() { ShowWeekend = false, InactiveCourseMode = "dim" } };
        var updated = AuthSettingsCatalog.Apply(current, new PreferencesRequest { Ui = new() { InactiveCourseMode = mode } });
        Assert.False(updated.Ui.ShowWeekend);
        Assert.Equal("dim", updated.Ui.InactiveCourseMode);
        Assert.Equal("dim", AuthSettingsCatalog.Normalize(new AppSettingsDocument { Ui = new() { InactiveCourseMode = mode! } }).Ui.InactiveCourseMode);
    }

    [Fact]
    public async Task PreferencesEndpoint_PersistsUiAndRetainsScheduleAndNetworkOnPartialUpdates()
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(data.RootPath);
        const string sid = "test-preferences-session";
        root.Credentials().Save(new StoredCredentialDocument { User = "20260001", StudentId = "20260001", Password = "fake-password" });
        root.Sessions().Save(sid, new StoredSessionDocument { User = "20260001", StudentId = "20260001" });
        var student = root.ForStudent("20260001");
        student.SaveProfile(new StudentProfileDocument { StudentId = "20260001", Name = "Test Student" });
        student.SavePreferences(AuthSettingsCatalog.CreateDefault());
        await using (var host = await WebHostTestServer.StartAsync(web, data))
        {
            host.Client.DefaultRequestHeaders.Add("X-XKE-Session", sid);
            using var first = await host.Client.PostAsJsonAsync("/api/auth/preferences", new
            {
                schedule = new { academic_term = "2026-2027-1", manual_week = "8", cookie_rq = "2026-10-07", week_mode = "manual" },
                network = new { provider = "telecom" },
                ui = new { show_weekend = false, inactive_course_mode = "dim" },
            });
            first.EnsureSuccessStatusCode();
            using var second = await host.Client.PostAsJsonAsync("/api/auth/preferences", new
            { schedule = new { auto_refresh_enabled = true }, network = new { heartbeat_enabled = true }, ui = new { inactive_course_mode = "invalid" } });
            second.EnsureSuccessStatusCode();
            using var payload = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            var settings = payload.RootElement.GetProperty("data").GetProperty("settings");
            Assert.Equal("2026-2027-1", settings.GetProperty("schedule").GetProperty("academic_term").GetString());
            Assert.Equal("8", settings.GetProperty("schedule").GetProperty("manual_week").GetString());
            Assert.False(settings.GetProperty("ui").GetProperty("show_weekend").GetBoolean());
            Assert.Equal("dim", settings.GetProperty("ui").GetProperty("inactive_course_mode").GetString());
            using var badType = await host.Client.PostAsJsonAsync("/api/auth/preferences", new { ui = new { show_weekend = "not-a-bool" } });
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, badType.StatusCode);
        }
        var stored = student.LoadPreferences()!;
        Assert.Equal("telecom", stored.Network.Provider);
        Assert.False(stored.Ui.ShowWeekend);
        Assert.Equal("dim", stored.Ui.InactiveCourseMode);
        Assert.Equal("2026-2027-1", stored.Schedule.AcademicTerm);
        Assert.Equal("2026-10-07", stored.Schedule.CookieRq);
        await using var restarted = await WebHostTestServer.StartAsync(web, data);
        restarted.Client.DefaultRequestHeaders.Add("X-XKE-Session", sid);
        using var get = await restarted.Client.GetAsync("/api/auth/preferences");
        get.EnsureSuccessStatusCode();
        using var restored = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        var ui = restored.RootElement.GetProperty("data").GetProperty("settings").GetProperty("ui");
        Assert.False(ui.GetProperty("show_weekend").GetBoolean());
        Assert.Equal("dim", ui.GetProperty("inactive_course_mode").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_InvalidIntervalsKeepExistingValues(int value)
    {
        var settings = AuthSettingsCatalog.Apply(AuthSettingsCatalog.CreateDefault(), new PreferencesRequest
        {
            Schedule = new ScheduleSettingsRequest { AutoRefreshIntervalHours = value, LessonReminderMinutes = value },
            Network = new NetworkSettingsRequest { HeartbeatIntervalMinutes = value },
        });
        Assert.Equal(4, settings.Schedule.AutoRefreshIntervalHours);
        Assert.Equal(30, settings.Schedule.LessonReminderMinutes);
        Assert.Equal(5, settings.Network.HeartbeatIntervalMinutes);
    }
}
