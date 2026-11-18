using CSharpFunctionalExtensions;

namespace Application.Dadata;

/// <summary>
/// Память одного прохода загрузки: одинаковый адрес спрашивается у DaData один раз.
/// Следующая накладная того же прохода получает готовый разбор, а если первая успела записать синоним — совпадёт по нему.
/// </summary>
public sealed class DadataLookupCache
{
    private readonly Dictionary<string, Result<DadataCleanOutcome>> _outcomes = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string address, out Result<DadataCleanOutcome> outcome) =>
        _outcomes.TryGetValue(Key(address), out outcome);

    public void Remember(string address, Result<DadataCleanOutcome> outcome) =>
        _outcomes[Key(address)] = outcome;

    // Сравнение без учёта регистра и краевых пробелов; внутренние пробелы адрес не нормализует.
    private static string Key(string address) => (address ?? "").Trim();
}
