namespace Xke.Storage;

public static class XkeDataRootFactory
{
    public static XkeDataRoot CreateDefault(string appRoot, string? workspaceRoot = null, string? explicitDataRoot = null)
    {
        var configured = explicitDataRoot
            ?? Environment.GetEnvironmentVariable("XKE_DOTNET_DATA_DIR");

        var root = string.IsNullOrWhiteSpace(configured)
            ? ResolveLocalAppDataRoot()
            : configured.Trim();

        return new XkeDataRoot(root, workspaceRoot ?? appRoot);
    }

    private static string ResolveLocalAppDataRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = (Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty).Trim();
        }

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LOCALAPPDATA is unavailable; set XKE_DOTNET_DATA_DIR explicitly.");
        }

        return Path.Combine(localAppData, ".XKE", "data");
    }
}
