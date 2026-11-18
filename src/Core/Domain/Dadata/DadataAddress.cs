using System.Text.Json.Serialization;

namespace Domain.Dadata;

/// <summary>
/// Тексты DaData одинаковы для разбора, настроек и HTTP-клиента.
/// </summary>
public static class DadataMessages
{
    public const string Disabled = "Интеграция с DaData выключена";
    public const string NoApiKey = "Укажите API-ключ DaData";
    public const string NoSecret = "Укажите секретный ключ DaData";
    public const string Unavailable = "DaData недоступна";
    public const string EmptyAddress = "Укажите адрес";
    public const string LongAddress = "Адрес DaData длиннее 250 символов";
    public const string HouseNotFound = "Дом по адресу не найден";
}

/// <summary>
/// Ответ подсказок DaData. Разбор в Application: здесь только то, что приходит в JSON.
/// </summary>
public sealed class DadataSuggestionResponse
{
    [JsonPropertyName("suggestions")]
    public List<DadataSuggestion> Suggestions { get; set; } = [];
}

/// <summary>
/// Строка подсказки: адрес и поля дома.
/// </summary>
public sealed class DadataSuggestion
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    /// <summary>
    /// Полный адрес строки. Именно он попадает в поле адреса торговой точки.
    /// </summary>
    [JsonPropertyName("unrestricted_value")]
    public string UnrestrictedValue { get; set; } = "";

    [JsonPropertyName("data")]
    public DadataSuggestionData? Data { get; set; }
}

/// <summary>
/// Поля дома внутри строки подсказки.
/// </summary>
public sealed class DadataSuggestionData
{
    [JsonPropertyName("house_fias_id")]
    public string HouseFiasId { get; set; } = "";

    /// <summary>
    /// 8 — дом. Остальные уровни адреса торговой точкой не считаются.
    /// </summary>
    [JsonPropertyName("fias_level")]
    public string FiasLevel { get; set; } = "";
}

/// <summary>
/// Строка ответа стандартизации DaData: поля плоские, в отличие от подсказок.
/// </summary>
public sealed class DadataCleanResult
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("house_fias_id")]
    public string HouseFiasId { get; set; } = "";

    [JsonPropertyName("fias_level")]
    public string FiasLevel { get; set; } = "";

    /// <summary>
    /// 0 — уверенный разбор. Поля нет — разбор не уверенный: значение по умолчанию 0 таким не считается.
    /// </summary>
    [JsonPropertyName("qc")]
    public int? Qc { get; set; }
}

/// <summary>
/// Тело ошибки DaData. Поля message нет — ответ без текста.
/// </summary>
public sealed class DadataErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
