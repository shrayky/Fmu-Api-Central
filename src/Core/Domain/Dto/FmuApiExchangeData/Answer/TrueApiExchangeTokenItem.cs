using System.Text.Json.Serialization;

namespace Domain.Dto.FmuApiExchangeData.Answer;

public class TrueApiExchangeTokenItem
{
    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expired")]
    public DateTime Expired { get; set; }
}
