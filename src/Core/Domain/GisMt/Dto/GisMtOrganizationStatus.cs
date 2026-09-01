using System.Text.Json.Serialization;

namespace Domain.GisMt.Dto;

/// <summary>
/// Последний ответ Честного знака по организации.
/// </summary>
public class GisMtOrganizationStatus
{
    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("at")]
    public DateTime At { get; set; }
}
