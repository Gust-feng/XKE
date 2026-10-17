using Microsoft.Extensions.DependencyInjection;
using Xke.Contracts.Models;
using Xke.Core.Auth;
using Xke.Core.Schedule;
using Xke.Storage;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleWeekIdentityTests
{
    [Theory]
    [InlineData("第五周", "第5周", "2026-10-05")]
    [InlineData("第5周", "第5周", "2026-10-05")]
    [InlineData("5", "第5周", "2026-10-05")]
    [InlineData("第六周", "第6周", "2026-10-12")]
    public async Task LegacyChineseWeekAndDateOptions_ReturnOneCanonicalIdentity(string originalLabel, string expectedLabel, string expectedMonday)
    {
        using var data = new TemporaryDirectory();
        using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(data.RootPath);
        root.Credentials().Save(new StoredCredentialDocument
        {
            User = "20260001", Password = "synthetic-password", StudentId = "20260001", TrustedLoginToken = "synthetic-trust",
        });
        var student = root.ForStudent("20260001");
        student.SaveProfile(new StudentProfileDocument { StudentId = "20260001", Name = "测试同学" });
        student.SavePreferences(AuthSettingsCatalog.CreateDefault());
        student.Schedules().SaveSourceSchedule(new Dictionary<string, object?>
        {
            ["week_label"] = originalLabel, ["current_week_label"] = "第五周",
            ["viewed_week_number"] = ScheduleService.ParseWeekNumber(originalLabel), ["current_week_number"] = 5,
            ["source_key"] = "jw_schedule", ["week_dates"] = new Dictionary<string, string>(),
            ["available_weeks"] = new List<Dictionary<string, object?>>
            {
                new() { ["value"] = "2026-10-05", ["label"] = "第五周", ["selected"] = true },
                new() { ["value"] = "2026-10-12", ["label"] = "第六周", ["selected"] = false },
            },
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new() { ["week_label"] = originalLabel, ["course_name"] = "测试课程", ["weekday"] = "星期三", ["section"] = "1-2" },
            },
        }, new(), "jw_schedule", originalLabel);
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath,
            EnableBackgroundTasks = false, ScheduleApiOptions = new ScheduleApiOptions(),
        });
        var auth = app.Services.GetRequiredService<AuthService>();
        var login = auth.DesktopBootstrap();
        var payload = Assert.IsType<Dictionary<string, object?>>(login.Payload.CachedSchedule);
        Assert.Equal(expectedLabel, payload["week_label"]);
        Assert.Equal("第5周", payload["current_week_label"]);
        var options = Assert.IsType<List<object?>>(payload["available_weeks"]).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(new[] { "第5周", "第6周" }, options.Select(option => option["label"]?.ToString()).ToArray());
        Assert.Equal(new[] { "2026-10-05", "2026-10-12" }, options.Select(option => option["value"]?.ToString()).ToArray());
        Assert.Equal(expectedLabel, Assert.Single(options, option => (bool)option["selected"]!)["label"]);
        var dates = Assert.IsType<Dictionary<string, object?>>(payload["week_dates"]);
        Assert.Equal(7, dates.Count);
        Assert.Equal(expectedMonday, dates["星期一"]);
        var current = app.Services.GetRequiredService<ScheduleService>().LoadLocal(login.Payload.SessionId, expectedLabel, "preferred");
        Assert.Equal(expectedLabel, current.Payload["week_label"]);
    }

    [Fact]
    public void IsoDate_IsNotAWeekNumber()
    {
        Assert.Null(ScheduleService.ParseWeekNumber("2026-10-12"));
        Assert.Equal(6, ScheduleService.ParseWeekNumber("第六周"));
    }
}
