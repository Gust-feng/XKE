using System.Text.Json.Serialization;

namespace Xke.Contracts.Api;

public sealed record ApiEnvelope(
    [property: JsonPropertyName("ok")] bool Ok = true,
    [property: JsonPropertyName("msg")] string Msg = "",
    [property: JsonPropertyName("data")] object? Data = null
);
