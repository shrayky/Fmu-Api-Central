using System.Text.Json.Serialization;
using Domain.Dto.FmuApiExchangeData;

namespace Domain.Entitys.SettingsSchema.Dto;

public record SettingsSchemaView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("httpRequestTimeouts")]
    public HttpRequestTimeouts HttpRequestTimeouts { get; set; } = new();

    [JsonPropertyName("gisMtProductMappings")]
    public List<GisMtProductMapping> GisMtProductMappings { get; set; } = [];

    [JsonPropertyName("hostsToPing")]
    public List<StringValue> HostsToPing { get; set; } = [];
}
