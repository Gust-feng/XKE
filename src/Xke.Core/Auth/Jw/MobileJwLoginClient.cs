using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Models;

namespace Xke.Core.Auth.Jw;

public sealed class MobileJwLoginClient
{
    private static readonly Uri MobileBaseUri = new("http://222.243.161.213:81/hnrjzyxyhd/");
    private static readonly TimeSpan EncryptedPasswordTtl = TimeSpan.FromHours(1);
    private readonly HttpClient _client;

    public MobileJwLoginClient(HttpClient client)
    {
        _client = client;
        _client.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<MobileJwLoginResult> LoginAsync(
        string user,
        string password,
        MobileJwEncryptedCredentialDocument? cachedCredential = null,
        CancellationToken cancellationToken = default)
    {
        var cleanedUser = (user ?? string.Empty).Trim();
        var now = DateTimeOffset.Now;
        if (IsFresh(cachedCredential, now))
        {
            try
            {
                return await LoginWithEncryptedPasswordAsync(cleanedUser, cachedCredential!, cancellationToken).ConfigureAwait(false);
            }
            catch (MobileJwLoginException ex)
            {
                MobileJwDiagnosticsRecorder.RecordNote("mobile.login.cached.failed", new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reason"] = ex.Message,
                });
            }
        }

        var refreshedCredential = await CreateEncryptedCredentialAsync(cleanedUser, password, now, cancellationToken).ConfigureAwait(false);
        return await LoginWithEncryptedPasswordAsync(cleanedUser, refreshedCredential, cancellationToken).ConfigureAwait(false);
    }

    public static void ApplyMobileHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        request.Headers.Referrer = new Uri("http://222.243.161.213:81/hnrjzyxysjd/");
        request.Headers.TryAddWithoutValidation("Origin", "http://222.243.161.213:81");
        request.Headers.TryAddWithoutValidation("token", token);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
    }

    public static Uri AddQuery(Uri baseUri, IReadOnlyDictionary<string, string> values)
    {
        var query = string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{baseUri}?{query}");
    }

    private async Task<MobileJwEncryptedCredentialDocument> CreateEncryptedCredentialAsync(
        string user,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var keyUri = AddQuery(new Uri(MobileBaseUri, "getKey"), new Dictionary<string, string>
        {
            ["userNo"] = user,
        });

        using var keyRequest = new HttpRequestMessage(HttpMethod.Post, keyUri);
        ApplyMobileHeaders(keyRequest, token: string.Empty);
        using var keyResponse = await _client.SendAsync(keyRequest, cancellationToken).ConfigureAwait(false);
        using var keyDocument = await ReadJsonAsync(keyResponse, cancellationToken).ConfigureAwait(false);
        var keyRoot = keyDocument.RootElement;
        var key = ReadStringValue(keyRoot, "data", "result", "key", "aesKey", "aes_key");
        MobileJwDiagnosticsRecorder.RecordNote("mobile.getKey.result", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key_present"] = !string.IsNullOrWhiteSpace(key),
        });
        if (!IsSuccessCode(keyRoot) || string.IsNullOrWhiteSpace(key))
        {
            throw new MobileJwLoginException(ReadMessage(keyRoot, "获取动态密钥失败"));
        }

        // Mobile JW uses a dynamic-key AES ciphertext instead of captcha.
        // The ciphertext is DPAPI-protected by the credential store and reused for a short local TTL.
        return new MobileJwEncryptedCredentialDocument
        {
            EncryptedPassword = EncryptAppPassword(password, key),
            GeneratedAt = now.ToString("O"),
            ExpiresAt = now.Add(EncryptedPasswordTtl).ToString("O"),
        };
    }

    private async Task<MobileJwLoginResult> LoginWithEncryptedPasswordAsync(
        string user,
        MobileJwEncryptedCredentialDocument credential,
        CancellationToken cancellationToken)
    {
        var encryptedPassword = (credential.EncryptedPassword ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(encryptedPassword))
        {
            throw new MobileJwLoginException("移动教务加密密文为空");
        }

        var loginUri = AddQuery(new Uri(MobileBaseUri, "login"), new Dictionary<string, string>
        {
            ["userNo"] = user,
            ["pwd"] = encryptedPassword,
            ["encode"] = "1",
            ["captchaData"] = string.Empty,
            ["codeVal"] = string.Empty,
        });

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, loginUri);
        ApplyMobileHeaders(loginRequest, token: "null");
        using var loginResponse = await _client.SendAsync(loginRequest, cancellationToken).ConfigureAwait(false);
        using var loginDocument = await ReadJsonAsync(loginResponse, cancellationToken).ConfigureAwait(false);
        var loginRoot = loginDocument.RootElement;
        var token = ReadStringValue(loginRoot, "token", "accessToken", "access_token", "jwt", "sid");
        MobileJwDiagnosticsRecorder.RecordNote("mobile.login.result", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["token_present"] = !string.IsNullOrWhiteSpace(token),
        });

        if (!IsSuccessCode(loginRoot) || string.IsNullOrWhiteSpace(token))
        {
            throw new MobileJwLoginException(ReadMessage(loginRoot, "登录获取 Token 失败"));
        }

        return new MobileJwLoginResult(token, loginRoot.Clone(), credential);
    }

    private static bool IsFresh(MobileJwEncryptedCredentialDocument? credential, DateTimeOffset now)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.EncryptedPassword))
        {
            return false;
        }

        return DateTimeOffset.TryParse(credential.ExpiresAt, out var expiresAt) && expiresAt > now;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        MobileJwDiagnosticsRecorder.RecordResponse(
            response.RequestMessage?.RequestUri?.AbsolutePath.Trim('/').Replace('/', '-') ?? "mobile-response",
            response,
            body);
        if (!response.IsSuccessStatusCode)
        {
            throw new MobileJwLoginException($"教务接口返回 HTTP {(int)response.StatusCode}");
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new MobileJwLoginException($"教务接口返回了非 JSON 响应: {ex.Message}");
        }
    }

    private static string EncryptAppPassword(string password, string key)
    {
        var normalizedKey = (key ?? string.Empty).PadRight(16, '0')[..16];
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(normalizedKey);

        using var encryptor = aes.CreateEncryptor();
        var plaintext = Encoding.UTF8.GetBytes(password ?? string.Empty);
        var encrypted = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        var first = Convert.ToBase64String(encrypted);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(first));
    }

    private static bool IsSuccessCode(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "success", out var success))
        {
            if (success.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (success.ValueKind == JsonValueKind.String
                && string.Equals(success.GetString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (TryGetPropertyIgnoreCase(root, "ok", out var ok))
        {
            if (ok.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (ok.ValueKind == JsonValueKind.String
                && string.Equals(ok.GetString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!TryGetPropertyIgnoreCase(root, "code", out var code))
        {
            return false;
        }

        return code.ValueKind switch
        {
            JsonValueKind.String => IsSuccessCodeText(code.GetString()),
            JsonValueKind.Number when code.TryGetInt32(out var value) => value is 1 or 200,
            _ => false,
        };
    }

    private static bool IsSuccessCodeText(string? value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned is "1" or "200" || cleaned.Equals("success", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMessage(JsonElement root, string fallback)
    {
        var message = ReadStringValue(root, "Msg", "msg", "message", "Message", "error", "detail");
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    private static string ReadStringValue(JsonElement root, params string[] keys)
    {
        foreach (var source in EnumerateSources(root))
        {
            foreach (var key in keys)
            {
                if (!TryGetPropertyIgnoreCase(source, key, out var element))
                {
                    continue;
                }

                var value = ElementToString(element);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<JsonElement> EnumerateSources(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return payload;
        foreach (var wrapperKey in new[] { "data", "result", "payload", "body", "student", "studentInfo", "user", "userInfo", "info", "profile" })
        {
            if (!TryGetPropertyIgnoreCase(payload, wrapperKey, out var nested))
            {
                continue;
            }

            if (nested.ValueKind == JsonValueKind.Object)
            {
                yield return nested;
            }
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement source, string key, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(key, out value))
        {
            return true;
        }

        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string ElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => ReadStringValue(element, "name", "label", "text", "value", "title", "mc", "名称"),
            _ => string.Empty,
        };
    }
}

public sealed record MobileJwLoginResult(
    string Token,
    JsonElement ProfilePayload,
    MobileJwEncryptedCredentialDocument Credential);

public sealed class MobileJwLoginException : Exception
{
    public MobileJwLoginException(string message)
        : base(message)
    {
    }
}
