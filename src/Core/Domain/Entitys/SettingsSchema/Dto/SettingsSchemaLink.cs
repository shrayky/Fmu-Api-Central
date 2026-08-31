using System.Text.Json.Serialization;

namespace Domain.Entitys.SettingsSchema.Dto;

public record SettingsSchemaLink
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
