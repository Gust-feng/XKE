using System.Text.Json;

namespace Xke.Storage;

public static class JsonValueConverter
{
    public static Dictionary<string, object?> ToDictionary(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object ? (Dictionary<string, object?>)(ToObject(element) ?? new Dictionary<string, object?>()) : new Dictionary<string, object?>();
    }

    public static object? ToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.TryGetDouble(out var d) ? d : element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }
}
