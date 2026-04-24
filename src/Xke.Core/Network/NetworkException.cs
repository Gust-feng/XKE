namespace Xke.Core.Network;

public sealed class NetworkException : Exception
{
    public NetworkException(int statusCode, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Detail { get; }
}
