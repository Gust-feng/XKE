using System.Reflection;

namespace Xke.Core.Releases;

public static class AppRelease
{
    public const string PackageId = "XKE";
    public const string RepositoryUrl = "https://github.com/Gust-feng/XKE";
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    public static string Version => typeof(AppRelease).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion.Split('+')[0];

    public static bool IsProjectPage(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort && uri.UserInfo.Length == 0
            && (uri.AbsolutePath.Equals("/Gust-feng/XKE", StringComparison.OrdinalIgnoreCase)
                || uri.AbsolutePath.StartsWith("/Gust-feng/XKE/", StringComparison.OrdinalIgnoreCase));
}
