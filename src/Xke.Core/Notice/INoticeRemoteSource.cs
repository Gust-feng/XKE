namespace Xke.Core.Notice;

public interface INoticeRemoteSource
{
    Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed record NoticeRemoteContentResult(
    bool Success,
    byte[] Content,
    string Error
);
