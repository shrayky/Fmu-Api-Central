using System.Text.Json.Serialization;

namespace Domain.Entitys.SettingsSchema;

/// <summary>
/// Соответствие кода товарной группы Атол коду Честного знака (ГИС МТ).
/// </summary>
public class GisMtProductMapping
{
    [JsonPropertyName("atolCode")]
    public int AtolCode { get; set; }

    [JsonPropertyName("trueApiGroupId")]
    public int TrueApiGroupId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("checkSmp")]
    public bool CheckSmp { get; set; }
}
