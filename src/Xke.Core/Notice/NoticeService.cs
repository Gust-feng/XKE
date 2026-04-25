using Xke.Core.CloudSync;

namespace Xke.Core.Notice;

public sealed class NoticeService
{
    private readonly CloudFeedService _cloudFeedService;

    public NoticeService(CloudFeedService cloudFeedService)
    {
        _cloudFeedService = cloudFeedService;
    }

    public Task<Dictionary<string, object?>> LoadFeedAsync(
        string channel,
        string version,
        string trigger = "view",
        string studentId = "",
        CancellationToken cancellationToken = default)
    {
        return _cloudFeedService.LoadFeedAsync(channel, version, trigger, studentId, cancellationToken);
    }

    public Task<Dictionary<string, object?>> ResolveStudentAccessDecisionAsync(
        string studentId,
        string trigger = "view",
        CancellationToken cancellationToken = default)
    {
        return _cloudFeedService.ResolveAccessDecisionAsync(studentId, trigger, cancellationToken);
    }

    public Dictionary<string, object?> BuildBlockedDetail(string studentId, Dictionary<string, object?> decision)
    {
        return _cloudFeedService.BuildBlockedDetail(studentId, decision);
    }
}
