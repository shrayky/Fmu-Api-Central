using System.Text.Json.Serialization;

namespace Domain.Entitys.Organization.Dto;

/// <summary>
/// Ответ проверки перед удалением точки: используется ли она входящими документами.
/// </summary>
public sealed class SalePointUsageView
{
    [JsonPropertyName("used")]
    public bool Used { get; set; }
}
