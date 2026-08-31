using System.Text.Json.Serialization;

namespace Domain.Entitys.InstanceGroup.Dto;

public record GroupForceUpdateRequest
{
    [JsonPropertyName("groupIds")]
    public List<string> GroupIds { get; init; } = [];

    [JsonPropertyName("updateId")]
    public string UpdateId { get; init; } = string.Empty;
}
