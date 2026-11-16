using System.Text.Json.Serialization;

namespace Domain.Entitys.Organization;

/// <summary>
/// Вариант адреса точки, как его присылает поставщик. Uid не показывается и нужен только для правки строки.
/// </summary>
public sealed class SalePointAlias
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";
}

/// <summary>
/// Торговая точка организации. Хранится в карточке организации: отдельной коллекции и репозитория нет.
/// </summary>
public sealed class SalePoint
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("fiasId")]
    public string FiasId { get; set; } = "";

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("aliases")]
    public List<SalePointAlias> Aliases { get; set; } = [];
}

/// <summary>
/// Значение отбора точки в журнале: пусто — отбор не задан, none — документы без точки, иначе uid.
/// </summary>
public static class SalePointFilter
{
    public const string None = "none";

    public static (string Uid, bool Without) Parse(string? value)
    {
        var text = (value ?? "").Trim();
        if (string.Equals(text, None, StringComparison.OrdinalIgnoreCase))
            return ("", true);

        return (text, false);
    }
}

/// <summary>
/// Точка для списков и журналов: имя и признак «закрыта» приходят из карточки, а не из документа.
/// </summary>
public sealed class SalePointOption
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }
}
