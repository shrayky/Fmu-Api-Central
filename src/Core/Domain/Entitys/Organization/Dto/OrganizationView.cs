using Domain.Etrn;
using Domain.GisMt.Models;
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

    [JsonPropertyName("xApiKeyExpiresAt")]
    public DateTime? XApiKeyExpiresAt { get; set; }

    [JsonPropertyName("gisMtLastStatus")]
    public GisMtLastStatus GisMtLastStatus { get; set; } = new();

    [JsonPropertyName("gisMtProductGroups")]
    public List<GisMtConnectedProductGroup> GisMtProductGroups { get; set; } = [];

    [JsonPropertyName("trueApiIntegrationSettings")]
    public TrueApiIntegrationSettings TrueApiIntegrationSettings { get; set; } = new();

    [JsonPropertyName("vetisSettings")]
    public OrganizationVetisSettings? VetisSettings { get; set; }

    [JsonPropertyName("etrnConnections")]
    public List<EtrnConnection>? EtrnConnections { get; set; }
}
