using CSharpFunctionalExtensions;

namespace Domain.Dadata;

/// <summary>
/// Порт DaData. HTTP живёт в Infrastructure; Application разбирает ответ и вызывается из сценариев загрузки.
/// </summary>
public interface IDadataAddressClient
{
    /// <summary>
    /// Подсказки по адресу. Ключи и включённость проверяются до запроса.
    /// </summary>
    Task<Result<IReadOnlyList<DadataSuggestion>>> Suggest(string address, CancellationToken cancellationToken);

    /// <summary>
    /// Стандартизация адреса. Без секретного ключа запрос не уходит.
    /// </summary>
    Task<Result<IReadOnlyList<DadataCleanResult>>> Clean(string address, CancellationToken cancellationToken);
}
