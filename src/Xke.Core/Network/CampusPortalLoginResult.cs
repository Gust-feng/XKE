using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xke.Core.Network;

/// <summary>Parse data only: never execute JSONP or retain a raw portal response.</summary>
internal sealed record CampusPortalLoginResult(bool Accepted, int StatusCode, string ErrorKind, string Message)
{
    public static CampusPortalLoginResult Parse(string body, int statusCode, string account, string password)
    {
        var json = body.Trim();
        if (!json.StartsWith('{'))
        {
            var callback = Regex.Match(json, @"^[a-zA-Z_$][\w$]*\s*\((?<json>\{.*\})\)\s*;?\s*$", RegexOptions.Singleline);
            if (!callback.Success) return Invalid(statusCode);
            json = callback.Groups["json"].Value;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("result", out var result)) return Invalid(statusCode);
            var value = result.ValueKind switch { JsonValueKind.True => "1", JsonValueKind.False => "0", JsonValueKind.String or JsonValueKind.Number => result.ToString(), _ => "" };
            if (value is not ("0" or "1")) return Invalid(statusCode);
            var accepted = value == "1";
            if (accepted && statusCode is >= 200 and < 300)
                return new(true, statusCode, string.Empty, "认证请求已接受");
            if (accepted) return Invalid(statusCode);
            var message = ReadMessage(root);
            foreach (var secret in new[] { password, account }.Where(value => !string.IsNullOrEmpty(value))
                .SelectMany(value => new[] { value, Uri.EscapeDataString(value) }).OrderByDescending(value => value.Length))
                message = message.Replace(secret, "[已隐藏]", StringComparison.Ordinal);
            message = Regex.Replace(Regex.Replace(message, @"<[^>]*>", ""), @"\s+", " ").Trim();
            if (message.Length > 160) message = message[..160];
            var ipRestricted = (message.Contains("IP", StringComparison.OrdinalIgnoreCase) || message.Contains("地址", StringComparison.Ordinal))
                && new[] { "限制", "禁止", "绑定", "范围", "不允许", "不合法", "不匹配", "错误", "not allowed", "bound", "restrict", "invalid", "range", "error" }
                    .Any(part => message.Contains(part, StringComparison.OrdinalIgnoreCase));
            return new(false, statusCode, ipRestricted ? "ip_restricted" : statusCode >= 500 ? "server_error" : "auth_rejected",
                message.Length > 0 ? "认证失败：" + message : $"认证服务拒绝请求（HTTP {statusCode}）");
        }
        catch (JsonException) { return Invalid(statusCode); }
    }

    private static string ReadMessage(JsonElement root)
    {
        foreach (var key in new[] { "msg", "message", "error" })
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString()!;
        if (root.TryGetProperty("ret_code", out var code) && int.TryParse(code.ToString(), out var number))
            return $"服务器错误码：{number}";
        return string.Empty;
    }

    private static CampusPortalLoginResult Invalid(int statusCode) => new(false, statusCode, "response_error",
        statusCode is >= 200 and < 300 ? "认证服务返回了无法识别的结果，请重试" : $"认证服务请求失败（HTTP {statusCode}）");
}
