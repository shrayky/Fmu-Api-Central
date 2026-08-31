using System.Text.Json.Serialization;

namespace Domain.TrueApiIntegration.Dto;

public record TrueApiTokenView
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expired")]
    public DateTime Expired { get; set; }
}
