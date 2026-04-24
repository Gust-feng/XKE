namespace Xke.Core.Feedback;

public sealed record FeedbackTransportResult(string StoredKey);

public interface IFeedbackTransport
{
    Task<FeedbackTransportResult> SubmitAsync(
        string studentId,
        string feedbackId,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken = default);
}
