using System.Text.Json.Serialization;

namespace Domain.TrueApiIntegration;

public class DigitalSignature
{
    [JsonPropertyName("presentation")]
    public string Presentation { get; set; } = string.Empty;

    [JsonPropertyName("workUntil")]
    public DateTime WorkUntil { get; set; }

    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;
}
