using System.Text.Json.Serialization;

namespace Domain.Entitys.AlertTemplates;

public class AlertTemplateScheduleSlot
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;
}
