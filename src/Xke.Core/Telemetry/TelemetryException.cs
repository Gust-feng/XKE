namespace Xke.Core.Telemetry;

public sealed class TelemetryException : Exception
{
    public TelemetryException(int statusCode, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Detail { get; }
}
