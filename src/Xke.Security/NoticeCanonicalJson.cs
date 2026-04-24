using System.Text;
using System.Text.Json;

namespace Xke.Security;

public static class NoticeCanonicalJson
{
    public static byte[] Serialize(object? payload)
    {
        var json = JsonSerializer.Serialize(payload ?? new Dictionary<string, object?>(), new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        using var document = JsonDocument.Parse(json);
        var builder = new StringBuilder();
        WriteCanonical(document.RootElement, builder);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void WriteCanonical(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var properties = element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < properties.Length; index++)
                {
                    if (index > 0) builder.Append(',');
                    builder.Append(JsonSerializer.Serialize(properties[index].Name));
                    builder.Append(':');
                    WriteCanonical(properties[index].Value, builder);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var itemIndex = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (itemIndex++ > 0) builder.Append(',');
                    WriteCanonical(item, builder);
                }
                builder.Append(']');
                break;
            default:
                builder.Append(element.GetRawText());
                break;
        }
    }
}
