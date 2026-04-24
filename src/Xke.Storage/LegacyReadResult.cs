namespace Xke.Storage;

public sealed class LegacyReadResult<T>
{
    public bool Exists { get; init; }
    public bool IsSecureEnvelope { get; init; }
    public bool HasValue { get; init; }
    public T Value { get; init; } = default!;
    public string SourcePath { get; init; } = string.Empty;
}
