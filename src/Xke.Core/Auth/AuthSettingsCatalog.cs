using Xke.Contracts.Enums;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;

namespace Xke.Core.Auth;

public static class AuthSettingsCatalog
{
    public static AppSettingsDocument CreateDefault()
    {
        return new AppSettingsDocument
        {
            Appearance = new AppearanceSettingsDocument
            {
                Theme = string.Empty,
            },
            Schedule = new ScheduleSettingsDocument
            {
                Source = "preferred",
                DeveloperMode = false,
                WeekMode = "current",
                ManualWeek = string.Empty,
                CookieRq = string.Empty,
                TermLabel = string.Empty,
                AcademicTerm = string.Empty,
                MockDay = string.Empty,
                MockTime = string.Empty,
                AutoRefreshEnabled = false,
                AutoRefreshMode = "daily",
                AutoRefreshDailyTime = "20:00",
                AutoRefreshIntervalHours = 4,
                LessonReminderEnabled = false,
                LessonReminderMinutes = 30,
                WeekendAutoSwitchEnabled = false,
                IgnoredWeekendBlocks = new IgnoredWeekendBlocksDocument(),
            },
            Auth = new AuthSettingsDocument
            {
                PreloginMode = AppOptions.DefaultPreloginMode,
            },
            Network = new NetworkSettingsDocument
            {
                Provider = "campus",
                HeartbeatEnabled = false,
                HeartbeatIntervalMinutes = 5,
            },
            Ui = new UiSettingsDocument
            {
                Panels = new UiPanelsDocument
                {
                    Schedule = false,
                    LoginMode = false,
                    Appearance = false,
                },
            },
        };
    }

    public static AppSettingsDocument Normalize(AppSettingsDocument? raw)
    {
        var defaults = CreateDefault();
        raw ??= defaults;

        return new AppSettingsDocument
        {
            Appearance = new AppearanceSettingsDocument
            {
                Theme = NormalizeTheme(raw.Appearance?.Theme, defaults.Appearance.Theme),
            },
            Schedule = new ScheduleSettingsDocument
            {
                Source = NormalizeOption(raw.Schedule?.Source, AppOptions.ScheduleSources, defaults.Schedule.Source),
                DeveloperMode = raw.Schedule?.DeveloperMode ?? defaults.Schedule.DeveloperMode,
                WeekMode = NormalizeOption(raw.Schedule?.WeekMode, AppOptions.WeekModes, defaults.Schedule.WeekMode),
                ManualWeek = (raw.Schedule?.ManualWeek ?? defaults.Schedule.ManualWeek).Trim(),
                CookieRq = (raw.Schedule?.CookieRq ?? defaults.Schedule.CookieRq).Trim(),
                TermLabel = (raw.Schedule?.TermLabel ?? defaults.Schedule.TermLabel).Trim(),
                AcademicTerm = (raw.Schedule?.AcademicTerm ?? defaults.Schedule.AcademicTerm).Trim(),
                MockDay = (raw.Schedule?.MockDay ?? defaults.Schedule.MockDay).Trim(),
                MockTime = (raw.Schedule?.MockTime ?? defaults.Schedule.MockTime).Trim(),
                AutoRefreshEnabled = raw.Schedule?.AutoRefreshEnabled ?? defaults.Schedule.AutoRefreshEnabled,
                AutoRefreshMode = NormalizeOption(raw.Schedule?.AutoRefreshMode, AppOptions.AutoRefreshModes, defaults.Schedule.AutoRefreshMode),
                AutoRefreshDailyTime = NormalizeDailyTime(raw.Schedule?.AutoRefreshDailyTime, defaults.Schedule.AutoRefreshDailyTime),
                AutoRefreshIntervalHours = NormalizePositiveNumber(raw.Schedule?.AutoRefreshIntervalHours, defaults.Schedule.AutoRefreshIntervalHours, 8760),
                LessonReminderEnabled = raw.Schedule?.LessonReminderEnabled ?? defaults.Schedule.LessonReminderEnabled,
                LessonReminderMinutes = Math.Max(15, NormalizePositiveNumber(raw.Schedule?.LessonReminderMinutes, defaults.Schedule.LessonReminderMinutes)),
                WeekendAutoSwitchEnabled = raw.Schedule?.WeekendAutoSwitchEnabled ?? defaults.Schedule.WeekendAutoSwitchEnabled,
                IgnoredWeekendBlocks = NormalizeIgnoredWeekendBlocks(raw.Schedule?.IgnoredWeekendBlocks),
            },
            Auth = new AuthSettingsDocument
            {
                PreloginMode = NormalizePreloginMode(raw.Auth?.PreloginMode),
            },
            Network = new NetworkSettingsDocument
            {
                PortalUrl = NormalizeHttpUrl(raw.Network?.PortalUrl, defaults.Network.PortalUrl),
                ConnectivityCheckUrls = NormalizeCheckUrls(raw.Network?.ConnectivityCheckUrls, defaults.Network.ConnectivityCheckUrls),
                Provider = NormalizeOption(raw.Network?.Provider, AppOptions.NetworkProviders, defaults.Network.Provider),
                HeartbeatEnabled = raw.Network?.HeartbeatEnabled ?? defaults.Network.HeartbeatEnabled,
                HeartbeatIntervalMinutes = NormalizePositiveNumber(raw.Network?.HeartbeatIntervalMinutes, defaults.Network.HeartbeatIntervalMinutes),
            },
            Ui = new UiSettingsDocument
            {
                ShowWeekend = raw.Ui?.ShowWeekend ?? defaults.Ui.ShowWeekend,
                InactiveCourseMode = NormalizeOption(raw.Ui?.InactiveCourseMode, ["dim", "hide"], defaults.Ui.InactiveCourseMode),
                Panels = new UiPanelsDocument
                {
                    Schedule = raw.Ui?.Panels?.Schedule ?? defaults.Ui.Panels.Schedule,
                    LoginMode = raw.Ui?.Panels?.LoginMode ?? defaults.Ui.Panels.LoginMode,
                    Appearance = raw.Ui?.Panels?.Appearance ?? defaults.Ui.Panels.Appearance,
                },
            },
        };
    }

    public static AppSettingsDocument Apply(AppSettingsDocument current, PreferencesRequest request)
    {
        var normalized = Normalize(current);
        var appearanceTheme = normalized.Appearance.Theme;
        if (!string.IsNullOrWhiteSpace(request.Theme))
        {
            appearanceTheme = NormalizeTheme(request.Theme, appearanceTheme);
        }
        else if (request.Appearance is not null)
        {
            appearanceTheme = NormalizeTheme(request.Appearance.Theme, appearanceTheme);
        }

        var schedule = normalized.Schedule;
        if (request.Schedule is not null)
        {
            schedule = new ScheduleSettingsDocument
            {
                Source = NormalizeOption(request.Schedule.Source, AppOptions.ScheduleSources, schedule.Source),
                DeveloperMode = request.Schedule.DeveloperMode ?? schedule.DeveloperMode,
                WeekMode = NormalizeOption(request.Schedule.WeekMode, AppOptions.WeekModes, schedule.WeekMode),
                ManualWeek = request.Schedule.ManualWeek?.Trim() ?? schedule.ManualWeek,
                CookieRq = request.Schedule.CookieRq?.Trim() ?? schedule.CookieRq,
                TermLabel = request.Schedule.TermLabel?.Trim() ?? schedule.TermLabel,
                AcademicTerm = request.Schedule.AcademicTerm?.Trim() ?? schedule.AcademicTerm,
                MockDay = request.Schedule.MockDay?.Trim() ?? schedule.MockDay,
                MockTime = request.Schedule.MockTime?.Trim() ?? schedule.MockTime,
                AutoRefreshEnabled = request.Schedule.AutoRefreshEnabled ?? schedule.AutoRefreshEnabled,
                AutoRefreshMode = NormalizeOption(request.Schedule.AutoRefreshMode, AppOptions.AutoRefreshModes, schedule.AutoRefreshMode),
                AutoRefreshDailyTime = NormalizeDailyTime(request.Schedule.AutoRefreshDailyTime, schedule.AutoRefreshDailyTime),
                AutoRefreshIntervalHours = NormalizePositiveNumber(request.Schedule.AutoRefreshIntervalHours, schedule.AutoRefreshIntervalHours, 8760),
                LessonReminderEnabled = request.Schedule.LessonReminderEnabled ?? schedule.LessonReminderEnabled,
                LessonReminderMinutes = Math.Max(15, NormalizePositiveNumber(request.Schedule.LessonReminderMinutes, schedule.LessonReminderMinutes)),
                WeekendAutoSwitchEnabled = request.Schedule.WeekendAutoSwitchEnabled ?? schedule.WeekendAutoSwitchEnabled,
                IgnoredWeekendBlocks = request.Schedule.IgnoredWeekendBlocks is null
                    ? schedule.IgnoredWeekendBlocks
                    : NormalizeIgnoredWeekendBlocks(new IgnoredWeekendBlocksDocument
                    {
                        Saturday = request.Schedule.IgnoredWeekendBlocks.TryGetValue("saturday", out var saturday) && saturday is not null ? saturday : schedule.IgnoredWeekendBlocks.Saturday,
                        Sunday = request.Schedule.IgnoredWeekendBlocks.TryGetValue("sunday", out var sunday) && sunday is not null ? sunday : schedule.IgnoredWeekendBlocks.Sunday,
                    }),
            };
        }

        var auth = normalized.Auth;
        if (request.Auth is not null)
        {
            auth = new AuthSettingsDocument
            {
                PreloginMode = NormalizePreloginMode(request.Auth.PreloginMode),
            };
        }

        var network = normalized.Network;
        if (request.Network is not null)
        {
            network = new NetworkSettingsDocument
            {
                PortalUrl = NormalizeHttpUrl(request.Network.PortalUrl, network.PortalUrl),
                ConnectivityCheckUrls = NormalizeCheckUrls(request.Network.ConnectivityCheckUrls, network.ConnectivityCheckUrls),
                Provider = NormalizeOption(request.Network.Provider, AppOptions.NetworkProviders, network.Provider),
                HeartbeatEnabled = request.Network.HeartbeatEnabled ?? network.HeartbeatEnabled,
                HeartbeatIntervalMinutes = NormalizePositiveNumber(request.Network.HeartbeatIntervalMinutes, network.HeartbeatIntervalMinutes),
            };
        }

        var panels = normalized.Ui.Panels;
        if (request.Ui?.Panels is not null)
        {
            panels = new UiPanelsDocument
            {
                Schedule = request.Ui.Panels.Schedule ?? panels.Schedule,
                LoginMode = request.Ui.Panels.LoginMode ?? panels.LoginMode,
                Appearance = request.Ui.Panels.Appearance ?? panels.Appearance,
            };
        }

        return Normalize(new AppSettingsDocument
        {
            Appearance = new AppearanceSettingsDocument
            {
                Theme = appearanceTheme,
            },
            Schedule = schedule,
            Auth = auth,
            Network = network,
            Ui = new UiSettingsDocument
            {
                ShowWeekend = request.Ui?.ShowWeekend ?? normalized.Ui.ShowWeekend,
                InactiveCourseMode = NormalizeOption(request.Ui?.InactiveCourseMode, ["dim", "hide"], normalized.Ui.InactiveCourseMode),
                Panels = panels,
            },
        });
    }

    public static AppSettingsDocument WithPreloginMode(AppSettingsDocument settings, string preloginMode)
    {
        var normalized = Normalize(settings);
        return new AppSettingsDocument
        {
            Appearance = normalized.Appearance,
            Schedule = normalized.Schedule,
            Network = normalized.Network,
            Ui = normalized.Ui,
            Auth = new AuthSettingsDocument
            {
                PreloginMode = NormalizePreloginMode(preloginMode),
            },
        };
    }

    public static LegacyPreferencesDocument ToLegacyPreferences(AppSettingsDocument settings)
    {
        var normalized = Normalize(settings);
        return new LegacyPreferencesDocument
        {
            Theme = normalized.Appearance.Theme,
        };
    }

    private static string NormalizeTheme(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public static string NormalizePreloginMode(string? value)
    {
        return AppOptions.DefaultPreloginMode;
    }

    private static string NormalizeOption(string? value, IReadOnlyCollection<string> allowed, string fallback)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return fallback;
        }

        return allowed.Contains(cleaned, StringComparer.Ordinal) ? cleaned : fallback;
    }



    private static string NormalizeHttpUrl(string? value, string fallback)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : fallback;
    }

    private static IReadOnlyList<string> NormalizeCheckUrls(IReadOnlyList<string>? urls, IReadOnlyList<string> fallback)
    {
        if (urls is null) return fallback;
        var normalized = urls.Select(value => NormalizeHttpUrl(value, string.Empty))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return normalized.Length > 0 ? normalized : fallback;
    }

    private static int NormalizePositiveNumber(int? value, int fallback, int maximum = 1440)
    {
        return value is > 0 && value <= maximum ? value.Value : fallback;
    }

    private static string NormalizeDailyTime(string? value, string fallback)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return fallback;
        }

        var parts = cleaned.Split(':', 2);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var hours)
            || !int.TryParse(parts[1], out var minutes)
            || hours is < 0 or > 23
            || minutes is < 0 or > 59)
        {
            return fallback;
        }

        return $"{hours:00}:{minutes:00}";
    }

    private static string NormalizeOptionalText(string? raw, Func<string?, string> normalizer, string fallback)
    {
        if (raw is null) return fallback;
        var cleaned = raw;
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var normalized = normalizer(cleaned);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static IgnoredWeekendBlocksDocument NormalizeIgnoredWeekendBlocks(IgnoredWeekendBlocksDocument? value)
    {
        return new IgnoredWeekendBlocksDocument
        {
            Saturday = NormalizeBlocks(value?.Saturday),
            Sunday = NormalizeBlocks(value?.Sunday),
        };
    }

    private static IReadOnlyList<int> NormalizeBlocks(IEnumerable<int>? values)
    {
        return (values ?? Array.Empty<int>())
            .Where(value => value is >= 1 and <= 5)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
    }
}
