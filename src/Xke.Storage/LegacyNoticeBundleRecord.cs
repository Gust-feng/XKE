namespace Xke.Storage;

public sealed class LegacyNoticeBundleRecord
{
    public Dictionary<string, object?> Entry { get; init; } = new();
    public Dictionary<string, object?> Bundle { get; init; } = new();
}
