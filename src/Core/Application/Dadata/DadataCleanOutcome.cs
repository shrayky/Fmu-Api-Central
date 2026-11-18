namespace Application.Dadata;

/// <summary>
/// Результат стандартизации адреса: ФИАС дома и каноническая строка DaData.
/// Пустой ФИАС — разбор неуверенный, подбор точки по нему не идёт.
/// </summary>
public sealed class DadataCleanOutcome
{
    public static readonly DadataCleanOutcome Empty = new();

    public string FiasId { get; init; } = "";

    /// <summary>
    /// Стандартизованный адрес одной строкой. Заполняется только при уверенном разборе.
    /// </summary>
    public string Address { get; init; } = "";

    public bool Confident => FiasId.Length > 0;
}
