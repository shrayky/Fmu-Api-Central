using System.Text.Json.Serialization;

namespace Domain.Entitys.Organization.Dto;

/// <summary>
/// Тело назначения точки на документ: пустая строка очищает поле.
/// </summary>
public sealed class SalePointCommand
{
    [JsonPropertyName("salePointUid")]
    public string? SalePointUid { get; set; }
}
