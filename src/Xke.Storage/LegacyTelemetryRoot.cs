namespace Xke.Storage;

public sealed class LegacyTelemetryRoot
{
    public string DirectoryPath { get; }
    public string InstallationPath => Path.Combine(DirectoryPath, "installation.json");
    public string TelemetryAesKeyPath => Path.Combine(DirectoryPath, "telemetry_aes_key.json");
    public string SpoolDirectory => Path.Combine(DirectoryPath, "spool");

    public LegacyTelemetryRoot(string directoryPath)
    {
        DirectoryPath = directoryPath;
    }
}
