using System.Text.Json.Serialization;

namespace Xke.Contracts.Api;

public sealed record ApiErrorResponse(
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("detail")] string Detail)
{
    [JsonPropertyName("ok")] public bool Ok => false;
    [JsonPropertyName("data")] public object? Data => null;
}
