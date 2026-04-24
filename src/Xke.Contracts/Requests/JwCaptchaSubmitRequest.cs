using System.Text.Json.Serialization;

namespace Xke.Contracts.Requests;

public sealed class JwCaptchaSubmitRequest
{
    [JsonPropertyName("challenge_id")]
    public required string ChallengeId { get; init; }

    [JsonPropertyName("captcha_code")]
    public required string CaptchaCode { get; init; }
}
