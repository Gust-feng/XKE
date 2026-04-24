namespace Xke.Storage;

public sealed class LegacyNoticeValidationResult<T>
{
    public bool Exists { get; init; }
    public bool SignatureValid { get; init; }
    public bool HashValid { get; init; }
    public bool HasValue { get; init; }
    public T Value { get; init; } = default!;
    public string SourcePath { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}
