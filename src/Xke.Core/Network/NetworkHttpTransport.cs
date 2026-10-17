namespace Xke.Core.Network;

public static class NetworkHttpTransport
{
    // Campus authentication must not be carried through a system HTTP proxy or a redirected origin.
    // This does not override OS routes, VPNs or interface selection.
    public static HttpClientHandler CreateHandler() => new() { UseProxy = false, AllowAutoRedirect = false };
}
