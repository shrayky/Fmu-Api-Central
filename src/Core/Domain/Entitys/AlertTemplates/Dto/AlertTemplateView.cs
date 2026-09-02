using System.Text.Json.Serialization;

namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertTemplateView
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("script")]
    public string Script { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("scheduler")]
    public List<AlertTemplateScheduleSlot> Scheduler { get; init; } = [];
}
