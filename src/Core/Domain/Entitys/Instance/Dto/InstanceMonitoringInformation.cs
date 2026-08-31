using System.Text.Json.Serialization;
using Domain.Dto.FmuApiExchangeData.DataPacket.FmuApiState;
using Domain.Entitys.InstanceGroup.Dto;

namespace Domain.Entitys.Instance.Dto;

public record InstanceMonitoringInformation
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;
    
    [JsonPropertyName("id")]
    public string Token { get; init; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.MinValue;
    
    [JsonPropertyName("secretKey")]
    public string SecretKey { get; init; } = string.Empty;

    [JsonPropertyName("localModules")]
    public List<LocalModuleInformation> LocalModules { get; init; } = [];

    [JsonPropertyName("TsPiots")]
    public List<TsPiotInformation> TsPiots { get; init; } = [];

    [JsonPropertyName("forcedUpdateId")]
    public string ForcedUpdateId { get; init; } = string.Empty;

    [JsonPropertyName("group")]
    public GroupLink Group { get; init; } = new();

    [JsonPropertyName("settingsModified")]
    public bool SettingsModified { get; init; } = true;
}