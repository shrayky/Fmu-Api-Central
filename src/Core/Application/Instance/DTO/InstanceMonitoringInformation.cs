using System.Text.Json.Serialization;

namespace Application.Instance.DTO;

public record InstanceMonitoringInformation
{
    [JsonPropertyName("name")]
    public string Name = string.Empty;
    [JsonPropertyName("token")]
    public string Token = string.Empty;
    [JsonPropertyName("version")]
    public string Version = string.Empty;
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated = DateTime.MinValue;
    
}