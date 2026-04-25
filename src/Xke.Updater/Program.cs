using Xke.Core.Update;

var arguments = ParseArgs(args);
var parentPid = TryParseInt(arguments.GetValueOrDefault("pid"));
var planPath = arguments.GetValueOrDefault("plan") ?? string.Empty;
var executablePath = arguments.GetValueOrDefault("exe") ?? string.Empty;

if (string.IsNullOrWhiteSpace(planPath))
{
    Environment.ExitCode = 2;
    return;
}

try
{
    var result = await new UpdateInstaller().InstallAsync(new UpdateInstallerOptions(
        ParentProcessId: parentPid,
        PlanPath: planPath,
        MainExecutablePath: executablePath,
        RestartAfterInstall: true));
    Environment.ExitCode = result.Ok ? 0 : 1;
}
catch
{
    Environment.ExitCode = 1;
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index++)
    {
        var current = args[index];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = current[2..].Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            continue;
        }

        var value = index + 1 < args.Length ? args[index + 1] : string.Empty;
        if (!value.StartsWith("--", StringComparison.Ordinal))
        {
            index++;
        }
        else
        {
            value = string.Empty;
        }

        result[key] = value;
    }

    return result;
}

static int TryParseInt(string? value)
{
    return int.TryParse(value, out var parsed) ? parsed : 0;
}
