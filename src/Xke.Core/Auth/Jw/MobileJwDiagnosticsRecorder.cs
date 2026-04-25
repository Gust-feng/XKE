using System.Text.Json;
using Xke.Security;
using Xke.Storage;

namespace Xke.Core.Auth.Jw;

internal static class MobileJwDiagnosticsRecorder
{
    private static readonly object Gate = new();
    private static readonly LocalEncryptedFileStore EncryptedFiles = new();

    public static void RecordResponse(string step, HttpResponseMessage response, string body)
    {
        if (!TryResolveRoot(out var root))
        {
            return;
        }

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "raw"));

        var extension = LooksLikeJson(body) ? ".json" : ".txt";
        var fileName = $"{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}_{XkeDataRoot.SanitizePathSegment(step)}{extension}";
        var rawPath = Path.Combine(root, "raw", fileName);
        EncryptedFiles.WriteText(rawPath, body ?? string.Empty);

        // Raw remote payload capture is opt-in and only used by the real mobile validation tool.
        AppendEntry(root, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["recorded_at"] = DateTimeOffset.Now.ToString("O"),
            ["step"] = step,
            ["endpoint"] = response.RequestMessage?.RequestUri?.AbsoluteUri ?? string.Empty,
            ["status_code"] = (int)response.StatusCode,
            ["http_success"] = response.IsSuccessStatusCode,
            ["raw_path"] = rawPath,
        });
    }

    public static void RecordNote(string step, Dictionary<string, object?> metadata)
    {
        if (!TryResolveRoot(out var root))
        {
            return;
        }

        Directory.CreateDirectory(root);
        var payload = new Dictionary<string, object?>(metadata ?? new Dictionary<string, object?>(), StringComparer.Ordinal)
        {
            ["recorded_at"] = DateTimeOffset.Now.ToString("O"),
            ["step"] = step,
        };
        AppendEntry(root, payload);
    }

    public static string ResolveDirectory()
    {
        return TryResolveRoot(out var root) ? root : string.Empty;
    }

    private static void AppendEntry(string root, Dictionary<string, object?> payload)
    {
        var indexPath = Path.Combine(root, "steps.jsonl");
        var line = JsonSerializer.Serialize(payload);
        lock (Gate)
        {
            var current = File.Exists(indexPath)
                ? EncryptedFiles.ReadText(indexPath)
                : string.Empty;
            EncryptedFiles.WriteText(indexPath, current + line + Environment.NewLine);
        }
    }

    private static bool TryResolveRoot(out string root)
    {
        root = string.Empty;
        var explicitRoot = (Environment.GetEnvironmentVariable("XKE_MOBILE_DIAGNOSTICS_DIR") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            root = Path.GetFullPath(explicitRoot);
            return true;
        }

        var enabled = (Environment.GetEnvironmentVariable("XKE_MOBILE_DIAGNOSTICS") ?? string.Empty).Trim();
        if (!string.Equals(enabled, "1", StringComparison.Ordinal)
            && !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dataRoot = (Environment.GetEnvironmentVariable("XKE_DOTNET_DATA_DIR") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            return false;
        }

        root = Path.Combine(Path.GetFullPath(dataRoot), ".mobile_diag");
        return true;
    }

    private static bool LooksLikeJson(string body)
    {
        var text = (body ?? string.Empty).TrimStart();
        return text.StartsWith("{", StringComparison.Ordinal) || text.StartsWith("[", StringComparison.Ordinal);
    }
}
