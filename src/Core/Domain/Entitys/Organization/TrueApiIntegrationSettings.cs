using System.Text.Json.Serialization;

namespace Domain.Entitys.Organization;

public class TrueApiIntegrationSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("digitalSignature")]
    public string DigitalSignature { get; set; } = string.Empty;
}
