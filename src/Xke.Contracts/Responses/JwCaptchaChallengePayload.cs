using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record JwCaptchaChallengePayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("challenge_id")] string ChallengeId,
    [property: JsonPropertyName("captcha_image")] string CaptchaImage,
    [property: JsonPropertyName("expires_in_seconds")] int ExpiresInSeconds);
