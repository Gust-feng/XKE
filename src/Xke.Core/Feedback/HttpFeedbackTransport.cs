using System.Net.Http.Json;
using System.Text.Json;
using Xke.Core.CloudSync;
using Xke.Storage;

namespace Xke.Core.Feedback;

public sealed class HttpFeedbackTransport : IFeedbackTransport
{
    public const string DefaultBaseUrl = "https://example.invalid/xke-feedback/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly LocalFeedbackOutboxTransport _fallback;
    private readonly Uri _reportUri;
    private readonly bool _cloudEnabled;

    public HttpFeedbackTransport(XkeDataRoot dataRoot, HttpClient httpClient, string? baseUrl = null)
    {
        _httpClient = httpClient;
        _fallback = new LocalFeedbackOutboxTransport(dataRoot);
        var resolvedBaseUrl = ResolveBaseUrl(baseUrl);
        _cloudEnabled = !IsDefaultPlaceholder(resolvedBaseUrl);
        _reportUri = ResolveReportUri(resolvedBaseUrl);
    }

    public async Task<FeedbackTransportResult> SubmitAsync(
        string studentId,
        string feedbackId,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        if (!_cloudEnabled)
        {
            return await SaveFallbackAsync(studentId, feedbackId, payload, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_reportUri, BuildCloudPayload(payload), JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await SaveFallbackAsync(studentId, feedbackId, payload, cancellationToken).ConfigureAwait(false);
            }

            var storedKey = ParseStoredKey(text);
            return string.IsNullOrWhiteSpace(storedKey)
                ? await SaveFallbackAsync(studentId, feedbackId, payload, cancellationToken).ConfigureAwait(false)
                : new FeedbackTransportResult(storedKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return await SaveFallbackAsync(studentId, feedbackId, payload, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string ResolveBaseUrl(string? configured = null)
    {
        var privateOptions = PrivateCloudEndpointOptions.Load();
        var value = FirstNonEmpty(
            configured,
            Environment.GetEnvironmentVariable("XKE_FEEDBACK_CLOUD_ENDPOINT"),
            Environment.GetEnvironmentVariable("XKE_CLOUD_FEEDBACK_BASE_URL"),
            Environment.GetEnvironmentVariable("XKE_CLOUD_FUNCTION_FEEDBACK_BASE_URL"),
            privateOptions.FeedbackCloudEndpoint,
            DefaultBaseUrl);
        return value.Trim();
    }

    private static Dictionary<string, object?> BuildCloudPayload(Dictionary<string, object?> payload)
    {
        return new Dictionary<string, object?>(payload, StringComparer.Ordinal);
    }

    private static Uri ResolveReportUri(string baseUrl)
    {
        var value = baseUrl.TrimEnd('/');
        if (value.EndsWith("/f", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(value, UriKind.Absolute);
        }

        return new Uri($"{value}/f", UriKind.Absolute);
    }

    private static bool IsDefaultPlaceholder(string value)
    {
        return string.Equals(
            value.Trim().TrimEnd('/'),
            DefaultBaseUrl.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseStoredKey(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (!TryReadBoolean(root, "ok", out var ok) || !ok)
            {
                return string.Empty;
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return ReadString(data, "stored_key");
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private Task<FeedbackTransportResult> SaveFallbackAsync(
        string studentId,
        string feedbackId,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        return _fallback.SubmitAsync(studentId, feedbackId, payload, cancellationToken);
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        return property.ValueKind == JsonValueKind.String
               && bool.TryParse(property.GetString(), out value);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString()
            : string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
