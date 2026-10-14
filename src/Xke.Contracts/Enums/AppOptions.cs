namespace Xke.Contracts.Enums;

public static class AppOptions
{
    public static readonly string[] ScheduleSources = ["preferred"];
    public static readonly string[] WeekModes = ["current", "next", "manual"];
    public static readonly string[] AutoRefreshModes = ["daily", "interval"];
    public static readonly string[] NetworkProviders = ["campus", "telecom", "unicom", "mobile"];
    public const string DefaultPreloginMode = "auto";
    public static readonly string[] PreloginModes = [DefaultPreloginMode];
}
