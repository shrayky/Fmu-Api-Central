using System.Text.Json.Serialization;

namespace Domain.Entitys.Organization.Dto;

public record OrganizationView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("trueApiEnabled")]
    public bool TrueApiEnabled { get; set; }

    [JsonPropertyName("trueApiTokenReceived")]
    public bool TrueApiTokenReceived { get; set; }

    [JsonPropertyName("trueApiTokenExpired")]
    public DateTime? TrueApiTokenExpired { get; set; }

    [JsonPropertyName("trueApiIntegrationSettings")]
    public TrueApiIntegrationSettings TrueApiIntegrationSettings { get; set; } = new();
}
