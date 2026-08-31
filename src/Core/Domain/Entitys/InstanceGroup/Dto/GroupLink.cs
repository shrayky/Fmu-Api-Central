using System.Text.Json.Serialization;

namespace Domain.Entitys.InstanceGroup.Dto;

public record GroupLink
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
