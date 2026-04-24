using Xke.Storage;

namespace Xke.Core.Feedback;

public sealed class LocalFeedbackOutboxTransport : IFeedbackTransport
{
    private readonly XkeFeedbackOutboxStore _outbox;

    public LocalFeedbackOutboxTransport(XkeDataRoot dataRoot)
    {
        _outbox = dataRoot.FeedbackOutbox();
    }

    public Task<FeedbackTransportResult> SubmitAsync(
        string studentId,
        string feedbackId,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        var storedKey = _outbox.Save(studentId, feedbackId, payload);
        return Task.FromResult(new FeedbackTransportResult(storedKey));
    }
}
