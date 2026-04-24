namespace Xke.WebHost;

public static class WebHostRuntime
{
    public static bool BetaEnabled()
    {
        return false;
    }

    public static bool IsBetaExpired()
    {
        return false;
    }

    public static string BetaMessage()
    {
        return string.Empty;
    }

    public static object BetaPayload()
    {
        return new { enabled = false };
    }

    public static object RuntimeContext()
    {
        return new { channel = "dotnet8" };
    }
}
