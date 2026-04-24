using Xke.Core.Notice;

namespace Xke.WebHost.Tests;

internal sealed class MissingNoticeRemoteSource : INoticeRemoteSource
{
    public Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NoticeRemoteContentResult(false, Array.Empty<byte>(), "missing"));
    }
}

internal sealed class MemoryNoticeRemoteSource : INoticeRemoteSource
{
    private readonly Dictionary<string, byte[]> _files;

    public MemoryNoticeRemoteSource(Dictionary<string, byte[]> files)
    {
        _files = files;
    }

    public Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var normalized = (relativePath ?? string.Empty).Trim().TrimStart('/').Replace('\\', '/');
        return _files.TryGetValue(normalized, out var content)
            ? Task.FromResult(new NoticeRemoteContentResult(true, content, string.Empty))
            : Task.FromResult(new NoticeRemoteContentResult(false, Array.Empty<byte>(), $"missing: {normalized}"));
    }
}
