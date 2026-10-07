namespace Xke.Core.Notice;

public sealed class LocalOnlyNoticeRemoteSource : INoticeRemoteSource
{
    public Task<NoticeRemoteContentResult> GetBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NoticeRemoteContentResult(
            false,
            Array.Empty<byte>(),
            "本地开发模式已禁用公告与云端配置拉取"));
    }
}
