using System.Text.Json;
using Xke.Contracts.Models;

namespace Xke.Core.Auth.Jw;

public sealed class NativeJwAuthClient : IJwAuthClient
{
    private static readonly Uri MobileBaseUri = new("http://222.243.161.213:81/hnrjzyxyhd/");
    private readonly HttpClient _client;
    private readonly MobileJwLoginClient _loginClient;

    public NativeJwAuthClient(HttpClient client)
        : this(client, new MobileJwLoginClient(client))
    {
    }

    public NativeJwAuthClient(HttpClient client, MobileJwLoginClient loginClient)
    {
        _client = client;
        _loginClient = loginClient;
        _client.Timeout = TimeSpan.FromSeconds(15);
    }

    public Task<JwAuthResult> InitializeAsync(
        string user,
        string password,
        string preloginMode,
        CancellationToken cancellationToken = default,
        MobileJwEncryptedCredentialDocument? mobileJw = null)
    {
        return FetchProfileAsync(user, password, mobileJw, cancellationToken);
    }

    public Task<JwAuthResult> ConnectAsync(
        string user,
        string password,
        string preloginMode,
        CancellationToken cancellationToken = default,
        MobileJwEncryptedCredentialDocument? mobileJw = null)
    {
        return FetchProfileAsync(user, password, mobileJw, cancellationToken);
    }

    private async Task<JwAuthResult> FetchProfileAsync(
        string user,
        string password,
        MobileJwEncryptedCredentialDocument? mobileJw,
        CancellationToken cancellationToken)
    {
        MobileJwLoginResult login;
        try
        {
            login = await _loginClient.LoginAsync(user, password, mobileJw, cancellationToken).ConfigureAwait(false);
        }
        catch (MobileJwLoginException ex)
        {
            throw new JwAuthException(ex.Message);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(MobileBaseUri, "student/my"));
        MobileJwLoginClient.ApplyMobileHeaders(request, login.Token);

        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!IsSuccessCode(root))
        {
            throw new JwAuthException(ReadMessage(root, "学生资料接口返回异常"));
        }

        // Mobile login can carry profile fields that student/my omits on some
        // real deployments. Normalize both responses as one authoritative view.
        var profile = StudentProfileNormalizer.FromJson(user, root, login.ProfilePayload);
        MobileJwDiagnosticsRecorder.RecordNote("auth.profile.result", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["student_id_present"] = !string.IsNullOrWhiteSpace(profile.StudentId),
            ["name_present"] = !string.IsNullOrWhiteSpace(profile.Name),
            ["class_present"] = !string.IsNullOrWhiteSpace(profile.ClassName),
            ["major_present"] = !string.IsNullOrWhiteSpace(profile.MajorName),
            ["academy_present"] = !string.IsNullOrWhiteSpace(profile.AcademyName),
        });
        return new JwAuthResult(profile, login.Token, login.Credential);
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
            throw new JwAuthException($"教务接口返回 HTTP {(int)response.StatusCode}");
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new JwAuthException($"教务接口返回了非 JSON 响应: {ex.Message}");
        }
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
            JsonValueKind.Number => code.TryGetInt32(out var value) && (value == 1 || value == 200),
            _ => false,
        };
    }

    private static string ReadMessage(JsonElement root, string fallback)
    {
        foreach (var source in EnumerateSources(root))
        {
            foreach (var key in new[] { "Msg", "msg", "message", "error", "errMsg", "errmsg" })
            {
                if (!TryGetPropertyIgnoreCase(source, key, out var element) || element.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var message = element.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }
            }
        }

        return fallback;
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

    private static IEnumerable<JsonElement> EnumerateSources(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return root;
        foreach (var key in new[] { "data", "result", "payload", "body", "student", "studentInfo", "student_info", "user", "userInfo", "user_info" })
        {
            if (!TryGetPropertyIgnoreCase(root, key, out var nested))
            {
                continue;
            }

            if (nested.ValueKind == JsonValueKind.Object)
            {
                yield return nested;
            }
            else if (nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in nested.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        yield return item;
                        break;
                    }
                }
            }
        }
    }

    private static string ElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty,
        };
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

    private static bool IsSuccessCodeText(string? value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned is "1" or "200" || cleaned.Equals("success", StringComparison.OrdinalIgnoreCase);
    }
}
