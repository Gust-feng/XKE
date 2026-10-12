using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Xke.Core.Schedule;

internal static class JwHtml
{
    public static IHtmlDocument Parse(string html) => new HtmlParser().ParseDocument(html);
    public static string Text(INode? node) => Regex.Replace(node?.TextContent ?? string.Empty, @"\s+", " ").Trim();

    public static bool IsSessionExpired(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                if (root.TryGetProperty("flag1", out var flag) && flag.ValueKind == JsonValueKind.Number && flag.TryGetInt32(out var value) && value == 2)
                    return true;
            }
            catch (JsonException) { }
        }
        return body.Contains("login-page-flowkey", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(body, "<input\\b[^>]*type\\s*=\\s*[\"']password[\"']", RegexOptions.IgnoreCase);
    }

    public static void EnsureAuthenticated(string body)
    {
        if (IsSessionExpired(body)) throw new TraditionalJwSessionExpiredException("教务会话已失效，请重新连接");
    }
}
