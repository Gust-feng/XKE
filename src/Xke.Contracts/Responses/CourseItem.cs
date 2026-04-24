namespace Xke.Contracts.Responses;

public sealed class CourseItem
{
    [System.Text.Json.Serialization.JsonPropertyName("kch")]
    public string Kch { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("kcmc")]
    public string Kcmc { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("xf")]
    public string Xf { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("skls")]
    public string Skls { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("sksj")]
    public string Sksj { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("skdd")]
    public string Skdd { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("xqmc")]
    public string Xqmc { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("xkrs")]
    public string Xkrs { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("syrs")]
    public string Syrs { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("ctsm")]
    public string Ctsm { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("szkcflmc")]
    public string Szkcflmc { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("jx02id")]
    public string Jx02Id { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("jx0404id")]
    public string Jx0404Id { get; init; } = string.Empty;
}
