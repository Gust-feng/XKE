namespace Xke.Security;

public static class SecurityAssetPaths
{
    public static string ResolveNoticeSigningPublicKeyPath(string appRoot)
    {
        return ResolveByAncestorSearch(appRoot, "core", "notice", "notice_signing_public.pem");
    }

    public static string ResolveRemoteSensingPublicKeyPath(string appRoot)
    {
        return ResolveByAncestorSearch(appRoot, "core", "remote_sensing", "public_key.pem");
    }

    private static string ResolveByAncestorSearch(string appRoot, params string[] segments)
    {
        var current = new DirectoryInfo(Path.GetFullPath(appRoot));
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(new[] { Path.GetFullPath(appRoot) }.Concat(segments).ToArray());
    }
}
