namespace Xke.Contracts.Requests;

public sealed class ProfileRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("nickname")]
    public string Nickname { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("use_nickname")]
    public bool UseNickname { get; init; } = true;
}
