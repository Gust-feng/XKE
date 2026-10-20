using AngleSharp.Html.Parser;
using Xke.Core.Schedule;

namespace Xke.Core.Auth.Jw;

public enum JwLoginFailureKind
{
    Unknown,
    CaptchaRejected,
    CredentialsRejected,
}

/// <summary>
/// School form-login failures are returned as HTTP 200 login pages, not distinct
/// HTTP status codes. Only the observed #showMsg messages permit classification;
/// unknown pages, lockout notices and ambiguous messages must never imply retry.
/// </summary>
public sealed class JwLoginException : JwScheduleException
{
    private JwLoginException(JwLoginFailureKind kind, string message) : base(message, 401)
    {
        Kind = kind;
    }

    public JwLoginFailureKind Kind { get; }

    internal static JwLoginException FromResponse(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var text = document.QuerySelector("#showMsg")?.TextContent ?? string.Empty;
        var normalized = string.Concat(text.Where(character => !char.IsWhiteSpace(character)))
            .Replace('，', ',').TrimEnd('!', '！');
        // Confirmed by bounded school requests on 2026-10-19. Do not infer a
        // captcha failure just because saved credentials worked in the past.
        return normalized switch
        {
            "验证码错误" => new(JwLoginFailureKind.CaptchaRejected, "教务验证码错误，请重新验证"),
            "该帐号不存在或密码错误,请联系管理员" => new(JwLoginFailureKind.CredentialsRejected, "教务账号或密码错误，请检查登录信息"),
            _ => new(JwLoginFailureKind.Unknown, "教务登录未进入 JW 工作区，请检查账号密码或验证码。阶段=jw_login_submit"),
        };
    }
}
