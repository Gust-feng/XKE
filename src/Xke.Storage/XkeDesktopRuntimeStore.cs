using System.Diagnostics;
using System.Text.Json;

namespace Xke.Storage;

public sealed class XkeDesktopRuntimeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;

    public XkeDesktopRuntimeStore(XkeDataRoot root)
    {
        _root = root;
    }

    public string RuntimeInstancePath => Path.Combine(_root.DesktopDirectory, "runtime_instance.json");

    public DesktopRuntimeInstance Load()
    {
        if (!File.Exists(RuntimeInstancePath))
        {
            return DesktopRuntimeInstance.Empty;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<DesktopRuntimeInstance>(File.ReadAllText(RuntimeInstancePath), JsonOptions);
            return payload ?? DesktopRuntimeInstance.Empty;
        }
        catch
        {
            return DesktopRuntimeInstance.Empty;
        }
    }

    public string Save(int port, int? processId = null)
    {
        Directory.CreateDirectory(_root.DesktopDirectory);
        // Persist the loopback endpoint so a second DesktopHost process can activate the first one.
        var payload = new DesktopRuntimeInstance
        {
            ProcessId = processId ?? Environment.ProcessId,
            Port = port,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        File.WriteAllText(RuntimeInstancePath, JsonSerializer.Serialize(payload, JsonOptions));
        return _root.RelativePath(RuntimeInstancePath);
    }

    public void Clear(int? expectedProcessId = null)
    {
        if (expectedProcessId.HasValue)
        {
            var current = Load();
            if (current.ProcessId != 0 && current.ProcessId != expectedProcessId.Value)
            {
                return;
            }
        }

        if (File.Exists(RuntimeInstancePath))
        {
            File.Delete(RuntimeInstancePath);
        }
    }
}

public sealed class DesktopRuntimeInstance
{
    public static DesktopRuntimeInstance Empty { get; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("pid")]
    public int ProcessId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("port")]
    public int Port { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;

    public bool HasEndpoint => Port > 0;
}
