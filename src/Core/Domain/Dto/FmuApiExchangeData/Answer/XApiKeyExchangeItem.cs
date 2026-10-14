using System.Text.Json.Serialization;

namespace Domain.Dto.FmuApiExchangeData.Answer;

public class XApiKeyExchangeItem
{
    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("xApiKey")]
    public string XApiKey { get; set; } = string.Empty;

    [JsonPropertyName("xApiKeyExpired")]
    public bool XApiKeyExpired { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
}
