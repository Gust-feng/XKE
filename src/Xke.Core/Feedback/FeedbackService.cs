using System.Security.Cryptography;
using System.Text;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Feedback;

public sealed class FeedbackService
{
    private readonly XkeDataRoot _dataRoot;
    private readonly IFeedbackTransport _transport;

    public FeedbackService(XkeDataRoot dataRoot, IFeedbackTransport transport)
    {
        _dataRoot = dataRoot;
        _transport = transport;
    }

    public async Task<(string Message, FeedbackReportPayload Payload)> ReportAsync(
        string sessionId,
        FeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = LoadSession(sessionId);
        var content = (request.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new FeedbackException(400, "反馈内容不能为空");
        }

        var now = DateTimeOffset.Now.ToString("O");
        var feedbackId = Guid.NewGuid().ToString("N");
        var installation = _dataRoot.Telemetry().LoadOrCreateInstallationProfile();
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "2026-04-23",
            ["feedback_id"] = feedbackId,
            ["reported_at"] = now,
            ["student_id"] = session.StudentId,
            ["student_id_hash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session.StudentId))).ToLowerInvariant(),
            ["install_uuid"] = installation.GetValueOrDefault("install_uuid")?.ToString() ?? string.Empty,
            ["category"] = Truncate(request.Category, 32, "other"),
            ["content"] = Truncate(content, 2000, string.Empty),
            ["contact"] = Truncate(request.Contact, 256, string.Empty),
            ["app_version"] = Truncate(request.Build, 64, string.Empty),
            ["request_context"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["current_tab"] = Truncate(request.CurrentTab, 64, string.Empty),
            },
            ["source"] = "desktop-feedback",
        };

        // Current slice writes to the local outbox only. Real cloud submission is
        // intentionally deferred to a later transport slice.
        var result = await _transport.SubmitAsync(session.StudentId, feedbackId, payload, cancellationToken).ConfigureAwait(false);
        return (
            "反馈已提交",
            new FeedbackReportPayload(
                FeedbackId: feedbackId,
                StoredKey: result.StoredKey,
                ReportedAt: now));
    }

    private SessionContext LoadSession(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new FeedbackException(401, "会话不存在或已过期，请重新登录");
        }

        var session = _dataRoot.Sessions().Load(cleaned)
            ?? throw new FeedbackException(401, "会话不存在或已过期，请重新登录");
        return new SessionContext(string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId);
    }

    private static string Truncate(string value, int maxLength, string fallback)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return fallback;
        }

        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private sealed record SessionContext(string StudentId);
}
