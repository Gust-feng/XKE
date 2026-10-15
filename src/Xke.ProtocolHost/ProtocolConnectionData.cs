using System.Text.Json.Serialization;
using Xke.Contracts.Models;

namespace Xke.ProtocolHost;

public sealed record ProtocolConnectionData(
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("student")] StudentProfileDocument Student);
