using System.Text.Json.Serialization;

namespace Domain.GisMt.Dto;

/// <summary>
/// Тело ручной операции POST /api/gismt/* от Central. Callback не передаём.
/// </summary>
public class GisMtManualOperationRequest
{
    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("dateFrom")]
    public DateTime? DateFrom { get; set; }

    [JsonPropertyName("dateTo")]
    public DateTime? DateTo { get; set; }
}
