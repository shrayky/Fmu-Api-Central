using System.Text.Json.Serialization;
using Domain.Dto.fmuApiStateInformation;

namespace Domain.Dto;

public record FmuApiPacket
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;
    [JsonPropertyName("architecture")]
    public string Architecture {get; init;} = string.Empty;
    [JsonPropertyName("os")]
    public string Os {get; init;} = string.Empty;
    [JsonPropertyName("parameters")]
    public FmuApiParameters Parameters { get; init; } = new();
    [JsonPropertyName("cdnData")]
    public List<CdnData> CdnData { get; init; } = [];
}