namespace Xke.Storage;

public static class XkeDataRootFactory
{
    public static XkeDataRoot CreateDefault(string appRoot, string? workspaceRoot = null, string? explicitDataRoot = null)
    {
        var configured = explicitDataRoot
            ?? Environment.GetEnvironmentVariable("XKE_DOTNET_DATA_DIR");

        // Active .NET runtime state must never fall back to Python legacy data
        // roots. LegacyDataRootFactory is reference-only for compatibility reads.
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetFullPath(appRoot), ".runtime", "data")
            : configured.Trim();

        return new XkeDataRoot(root, workspaceRoot ?? appRoot);
    }
}
