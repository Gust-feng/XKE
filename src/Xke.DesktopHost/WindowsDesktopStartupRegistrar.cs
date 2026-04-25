using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Xke.Core.Desktop;

namespace Xke.DesktopHost;

internal sealed class WindowsDesktopStartupRegistrar : IDesktopStartupRegistrar
{
    public const string StartupArgument = "--xke-startup";
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "XKE.DotNet8.DesktopHost";

    private readonly string _executablePath;

    public WindowsDesktopStartupRegistrar(string? executablePath = null)
    {
        _executablePath = ResolveExecutablePath(executablePath);
    }

    public bool? GetRegistrationState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        var value = key?.GetValue(ValueName)?.ToString() ?? string.Empty;
        return string.Equals(NormalizeCommand(value), NormalizeCommand(BuildCommand()), StringComparison.OrdinalIgnoreCase);
    }

    public void SetRegistrationState(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true)
            ?? throw new InvalidOperationException("无法打开当前用户开机启动注册表项。");

        if (enabled)
        {
            key.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private string BuildCommand()
    {
        if (string.IsNullOrWhiteSpace(_executablePath))
        {
            throw new InvalidOperationException("无法解析桌面程序路径，不能注册开机自启。");
        }

        return $"\"{_executablePath}\" {StartupArgument}";
    }

    private static string ResolveExecutablePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Path.GetFullPath(Environment.ProcessPath);
        }

        var processPath = Process.GetCurrentProcess().MainModule?.FileName;
        return string.IsNullOrWhiteSpace(processPath) ? string.Empty : Path.GetFullPath(processPath);
    }

    private static string NormalizeCommand(string value)
    {
        return (value ?? string.Empty).Trim().TrimEnd();
    }
}
