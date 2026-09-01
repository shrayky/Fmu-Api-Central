using System.Text.Json.Serialization;

namespace Domain.GisMt.Models;

/// <summary>
/// Последний ответ Честного знака по организации.
/// </summary>
public class GisMtLastStatus
{
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("at")]
    public DateTime? At { get; set; }
}
