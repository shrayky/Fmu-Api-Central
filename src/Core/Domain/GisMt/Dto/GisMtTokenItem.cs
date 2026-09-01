using System.Text.Json.Serialization;

namespace Domain.GisMt.Dto;

/// <summary>
/// Живой токен True API для одной организации.
/// </summary>
public class GisMtTokenItem
{
    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expired")]
    public DateTime Expired { get; set; }
}
