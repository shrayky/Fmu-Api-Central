using System.Text.Json.Serialization;

namespace Domain.Dto.fmuApiAnswer;

public record FmuApiAnswer
{
    [JsonPropertyName("configurationUpdateAvailable")]
    public bool ConfigurationUpdateAvailable  { get; init; } = false;
    [JsonPropertyName("softwareUpdateAvailable")]
    public bool SoftwareUpdateAvailable  { get; init; } = false;
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage  { get; init; } = string.Empty;
}