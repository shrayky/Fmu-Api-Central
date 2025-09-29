using System.Text.Json.Serialization;

namespace Application.Instance.DTO;

public record InstanceMonitoringInformation
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("id")]
    public string Token { get; init; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.MinValue;
    
    [JsonPropertyName("secretKey")]
    public string SecretKey { get; init; } = string.Empty;
    
}