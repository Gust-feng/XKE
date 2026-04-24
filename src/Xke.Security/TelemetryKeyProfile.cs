using System.Security.Cryptography;
using System.Text.Json;

namespace Xke.Security;

public sealed class TelemetryKeyProfile
{
    public string Algorithm { get; init; } = "AES-256-GCM";
    public byte[] KeyBytes { get; init; } = [];
    public string Fingerprint { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;

    public static TelemetryKeyProfile LoadFromDataRoot(string dataRootPath)
    {
        var path = Path.Combine(dataRootPath, ".telemetry", "telemetry_aes_key.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Telemetry AES key not found.", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var algorithm = root.TryGetProperty("algorithm", out var algorithmElement)
            ? (algorithmElement.GetString() ?? "AES-256-GCM").Trim()
            : "AES-256-GCM";
        var keyB64 = root.TryGetProperty("key_b64", out var keyElement)
            ? (keyElement.GetString() ?? string.Empty).Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(keyB64))
        {
            throw new InvalidDataException("Telemetry AES key payload missing key_b64.");
        }

        var keyBytes = Convert.FromBase64String(keyB64);
        var fingerprint = Convert.ToHexString(SHA256.HashData(keyBytes)).ToLowerInvariant()[..16];
        return new TelemetryKeyProfile
        {
            Algorithm = algorithm,
            KeyBytes = keyBytes,
            Fingerprint = fingerprint,
            SourcePath = path,
        };
    }
}
