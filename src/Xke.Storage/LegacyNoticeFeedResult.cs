namespace Xke.Storage;

public sealed class LegacyNoticeFeedResult
{
    public bool Exists { get; init; }
    public Dictionary<string, object?> Feed { get; init; } = new();
    public Dictionary<string, object?> Index { get; init; } = new();
    public Dictionary<string, object?> Config { get; init; } = new();
    public List<string> Warnings { get; init; } = [];
    public string SourcePath { get; init; } = string.Empty;
}
