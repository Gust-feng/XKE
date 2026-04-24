using Xke.Security;

namespace Xke.Storage;

public static class LegacyNoticeAccessPolicy
{
    public const string DefaultAccessPolicyMessage = NoticeCompatibility.DefaultAccessPolicyMessage;

    public static Dictionary<string, object?> Normalize(Dictionary<string, object?> rawPolicy)
    {
        return NoticeCompatibility.NormalizeAccessPolicy(rawPolicy);
    }

    public static Dictionary<string, object?> ResolveDecision(Dictionary<string, object?> accessPolicy, string studentId)
    {
        return NoticeCompatibility.ResolveAccessPolicyDecision(accessPolicy, studentId);
    }

    public static Dictionary<string, object?> FailClosedConfig()
    {
        return NoticeCompatibility.FailClosedConfig();
    }
}
