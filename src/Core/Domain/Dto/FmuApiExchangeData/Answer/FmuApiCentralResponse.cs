using System.Text.Json.Serialization;

namespace Domain.Dto.FmuApiExchangeData.Answer;

public record FmuApiCentralResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("settingsUpdateAvailable")]
    public bool SettingsUpdateAvailable { get; init; }
    
    [JsonPropertyName("softwareUpdateAvailable")]
    public bool SoftwareUpdateAvailable { get; init; }
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}