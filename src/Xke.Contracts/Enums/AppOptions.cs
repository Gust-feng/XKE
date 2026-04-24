namespace Xke.Contracts.Enums;

public static class AppOptions
{
    public static readonly string[] ThemeIds = ["campus-dawn", "forest-notes", "graphite-night"];
    public static readonly string[] ScheduleSources = ["preferred", "mobile", "cookie"];
    public static readonly string[] WeekModes = ["current", "next", "manual"];
    public static readonly string[] AutoRefreshModes = ["daily", "interval"];
    public static readonly string[] NetworkProviders = ["campus", "telecom", "unicom", "mobile"];
    public static readonly string[] PreloginModes = ["capture", "coded"];
    public static readonly int[] AutoRefreshIntervalHours = [3, 4, 5, 6];
    public static readonly int[] ReminderMinutes = [5, 10, 15, 20, 30];
    public static readonly int[] NetworkHeartbeatIntervals = [3, 5, 10, 15];
}
