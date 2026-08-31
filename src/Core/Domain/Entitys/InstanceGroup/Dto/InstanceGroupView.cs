using System.Text.Json.Serialization;
using Domain.Entitys.SettingsSchema.Dto;

namespace Domain.Entitys.InstanceGroup.Dto;

public record InstanceGroupView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("autoUpdateAllowed")]
    public bool AutoUpdateAllowed { get; set; }

    [JsonPropertyName("instancesTotal")]
    public int InstancesTotal { get; set; }

    [JsonPropertyName("instancesOnline")]
    public int InstancesOnline { get; set; }

    [JsonPropertyName("settingsSchema")]
    public SettingsSchemaLink SettingsSchema { get; set; } = new();
}
