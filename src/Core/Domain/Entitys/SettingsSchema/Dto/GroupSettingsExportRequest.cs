using System.Text.Json.Serialization;

namespace Domain.Entitys.SettingsSchema.Dto;

public record GroupSettingsExportRequest
{
    [JsonPropertyName("groupIds")]
    public List<string> GroupIds { get; init; } = [];
}
