using System.Text.Json.Serialization;

namespace Application.Dadata;

/// <summary>
/// Тело запроса кнопки «Получить» на вкладке торговых точек.
/// </summary>
public sealed class DadataAddressRequest
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = "";
}
