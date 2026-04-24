namespace Xke.Storage;

public static class LegacyDataRootFactory
{
    public static LegacyDataRoot CreateDefault(string appRoot)
    {
        // Reference-only compatibility helper. Active .NET runtime paths must use
        // XkeDataRootFactory and XKE_DOTNET_DATA_DIR, not Python legacy data roots.
        var configured = Environment.GetEnvironmentVariable("XKE_DATA_DIR")
            ?? Environment.GetEnvironmentVariable("XKE_PORTABLE_DATA_DIR");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new LegacyDataRoot(configured.Trim());
        }

        return new LegacyDataRoot(Path.Combine(appRoot, "data"));
    }
}
