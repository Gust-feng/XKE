using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Responses;
using Xke.Core.CloudSync;

namespace Xke.Core.Telemetry;

public sealed class HttpTelemetryTransport : ITelemetryTransport
{
    public const string DefaultBaseUrl = "https://example.invalid/xke-telemetry/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _reportUri;

    public HttpTelemetryTransport(HttpClient httpClient, string? baseUrl = null)
    {
        _httpClient = httpClient;
        _reportUri = ResolveReportUri(baseUrl);
    }

    public async Task<TelemetryCloudResult> ReportAsync(
        Dictionary<string, object?> payload,
        string skipReason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_reportUri, payload, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new TelemetryCloudResult(
                    Ok: false,
                    Queued: true,
                    Skipped: false,
                    Reason: $"cloud-http-{(int)response.StatusCode}");
            }

            return ParseCloudResponse(text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new TelemetryCloudResult(
                Ok: false,
                Queued: true,
                Skipped: false,
                Reason: "cloud-unavailable");
        }
    }

    public static string ResolveBaseUrl(string? configured = null)
    {
        var privateOptions = PrivateCloudEndpointOptions.Load();
        var value = FirstNonEmpty(
            configured,
            Environment.GetEnvironmentVariable("XKE_TELEMETRY_CLOUD_ENDPOINT"),
            Environment.GetEnvironmentVariable("XKE_CLOUD_FUNCTION_BASE_URL"),
            privateOptions.TelemetryCloudEndpoint,
            DefaultBaseUrl);
        return value.Trim();
    }

    private static Uri ResolveReportUri(string? configured)
    {
        var value = ResolveBaseUrl(configured).TrimEnd('/');
        if (value.EndsWith("/telemetry/report", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(value, UriKind.Absolute);
        }

        return new Uri($"{value}/telemetry/report", UriKind.Absolute);
    }

    private static TelemetryCloudResult ParseCloudResponse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var ok = TryReadBoolean(root, "ok", out var resolvedOk) && resolvedOk;
            var data = root.TryGetProperty("data", out var rawData) && rawData.ValueKind == JsonValueKind.Object
                ? rawData
                : default;
            var queued = data.ValueKind == JsonValueKind.Object && TryReadBoolean(data, "queued", out var resolvedQueued)
                ? resolvedQueued
                : false;
            var skipped = data.ValueKind == JsonValueKind.Object && TryReadBoolean(data, "skipped", out var resolvedSkipped)
                ? resolvedSkipped
                : false;
            var reason = data.ValueKind == JsonValueKind.Object
                ? ReadString(data, "reason")
                : string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = ReadString(root, "msg");
            }

            return new TelemetryCloudResult(
                Ok: ok,
                Queued: queued,
                Skipped: skipped,
                Reason: string.IsNullOrWhiteSpace(reason) ? (ok ? "uploaded" : "cloud-rejected") : reason);
        }
        catch (JsonException)
        {
            return new TelemetryCloudResult(
                Ok: false,
                Queued: true,
                Skipped: false,
                Reason: "cloud-invalid-response");
        }
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
