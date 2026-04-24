using System.Text.Json;
using Xke.Security;

namespace Xke.Storage;

public sealed class LegacyFileReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string? _dataRootPath;
    private readonly SecureJsonDecryptor _secureJsonDecryptor;

    public LegacyFileReader(string? dataRootPath = null, SecureJsonDecryptor? secureJsonDecryptor = null)
    {
        _dataRootPath = dataRootPath;
        _secureJsonDecryptor = secureJsonDecryptor ?? new SecureJsonDecryptor();
    }

    public LegacyReadResult<JsonElement> ReadJsonElement(string path, string expectedPurpose = "")
    {
        if (!File.Exists(path))
        {
            return new LegacyReadResult<JsonElement> { Exists = false, SourcePath = path };
        }

        var text = File.ReadAllText(path);
        var isSecureEnvelope = SecureJsonEnvelope.TryParse(text, out _);
        if (isSecureEnvelope)
        {
            if (string.IsNullOrWhiteSpace(_dataRootPath))
            {
                return new LegacyReadResult<JsonElement>
                {
                    Exists = true,
                    IsSecureEnvelope = true,
                    SourcePath = path,
                };
            }

            try
            {
                var value = _secureJsonDecryptor.DecryptJsonElement(text, _dataRootPath, expectedPurpose);
                return new LegacyReadResult<JsonElement>
                {
                    Exists = true,
                    IsSecureEnvelope = true,
                    HasValue = true,
                    Value = value,
                    SourcePath = path,
                };
            }
            catch
            {
                return new LegacyReadResult<JsonElement>
                {
                    Exists = true,
                    IsSecureEnvelope = true,
                    SourcePath = path,
                };
            }
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return new LegacyReadResult<JsonElement>
            {
                Exists = true,
                IsSecureEnvelope = false,
                HasValue = true,
                Value = document.RootElement.Clone(),
                SourcePath = path,
            };
        }
        catch (JsonException)
        {
            return new LegacyReadResult<JsonElement>
            {
                Exists = true,
                IsSecureEnvelope = false,
                SourcePath = path,
            };
        }
    }

    public LegacyReadResult<T> ReadJson<T>(string path, string expectedPurpose = "")
    {
        var elementResult = ReadJsonElement(path, expectedPurpose);
        if (!elementResult.Exists || !elementResult.HasValue)
        {
            return new LegacyReadResult<T>
            {
                Exists = elementResult.Exists,
                IsSecureEnvelope = elementResult.IsSecureEnvelope,
                SourcePath = path,
            };
        }

        try
        {
            var value = elementResult.Value.Deserialize<T>(JsonOptions);
            return new LegacyReadResult<T>
            {
                Exists = true,
                IsSecureEnvelope = elementResult.IsSecureEnvelope,
                HasValue = value is not null,
                Value = value!,
                SourcePath = path,
            };
        }
        catch (JsonException)
        {
            return new LegacyReadResult<T>
            {
                Exists = true,
                IsSecureEnvelope = elementResult.IsSecureEnvelope,
                SourcePath = path,
            };
        }
    }

    public LegacyReadResult<string> ReadText(string path, string expectedPurpose = "")
    {
        if (!File.Exists(path))
        {
            return new LegacyReadResult<string> { Exists = false, SourcePath = path };
        }

        var text = File.ReadAllText(path);
        var isSecureEnvelope = SecureJsonEnvelope.TryParse(text, out _);
        if (isSecureEnvelope)
        {
            if (string.IsNullOrWhiteSpace(_dataRootPath))
            {
                return new LegacyReadResult<string>
                {
                    Exists = true,
                    IsSecureEnvelope = true,
                    SourcePath = path,
                };
            }

            try
            {
                var decoded = _secureJsonDecryptor.DecryptToText(text, _dataRootPath, expectedPurpose);
                return new LegacyReadResult<string>
                {
                    Exists = true,
                    IsSecureEnvelope = true,
                    HasValue = true,
                    Value = decoded,
                    SourcePath = path,
                };
            }
            catch
            {
                return new LegacyReadResult<string>
                {
                    Exists = true,
                    IsSecureEnvelope = true,
                    SourcePath = path,
                };
            }
        }

        return new LegacyReadResult<string>
        {
            Exists = true,
            IsSecureEnvelope = false,
            HasValue = true,
            Value = text,
            SourcePath = path,
        };
    }
}
