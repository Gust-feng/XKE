namespace Xke.Security;

public static class NoticePublicKeyLoader
{
    public static string LoadPem(string appRoot)
    {
        var path = SecurityAssetPaths.ResolveNoticeSigningPublicKeyPath(appRoot);
        return File.ReadAllText(path);
    }
}
