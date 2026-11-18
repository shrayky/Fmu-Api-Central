using CSharpFunctionalExtensions;
using Domain.Dadata;

namespace Application.Tests.Fakes;

/// <summary>
/// Готовые ответы DaData: HTTP в тестах нет, порт отдаёт то, что положили.
/// </summary>
internal sealed class ScriptedDadataClient : IDadataAddressClient
{
    private readonly Queue<Result<IReadOnlyList<DadataSuggestion>>> _suggest = new();
    private readonly Queue<Result<IReadOnlyList<DadataCleanResult>>> _clean = new();

    public List<string> SuggestRequests { get; } = [];

    public List<string> CleanRequests { get; } = [];

    public int SuggestCalls => SuggestRequests.Count;

    public int CleanCalls => CleanRequests.Count;

    public void EnqueueSuggest(params DadataSuggestion[] suggestions) =>
        _suggest.Enqueue(Result.Success<IReadOnlyList<DadataSuggestion>>(suggestions.ToList()));

    public void EnqueueSuggestFailure(string error) =>
        _suggest.Enqueue(Result.Failure<IReadOnlyList<DadataSuggestion>>(error));

    public void EnqueueClean(string? houseFiasId, int qc = 0, string result = "") =>
        _clean.Enqueue(Result.Success<IReadOnlyList<DadataCleanResult>>(
        [
            new DadataCleanResult { HouseFiasId = houseFiasId ?? "", Qc = qc, Result = result }
        ]));

    public void EnqueueCleanFailure(string error) =>
        _clean.Enqueue(Result.Failure<IReadOnlyList<DadataCleanResult>>(error));

    public Task<Result<IReadOnlyList<DadataSuggestion>>> Suggest(
        string address,
        CancellationToken cancellationToken)
    {
        SuggestRequests.Add(address);
        var result = _suggest.Count > 0
            ? _suggest.Dequeue()
            : Result.Success<IReadOnlyList<DadataSuggestion>>([]);
        return Task.FromResult(result);
    }

    public Task<Result<IReadOnlyList<DadataCleanResult>>> Clean(
        string address,
        CancellationToken cancellationToken)
    {
        CleanRequests.Add(address);
        // По умолчанию разбор неуверенный: тест, которому нужен дом, кладёт ответ сам.
        var result = _clean.Count > 0
            ? _clean.Dequeue()
            : Result.Success<IReadOnlyList<DadataCleanResult>>([new DadataCleanResult { Qc = 3 }]);
        return Task.FromResult(result);
    }
}

internal static class DadataTestData
{
    public static DadataSuggestion Suggestion(string? houseFiasId, string address, string fiasLevel = "") => new()
    {
        Value = address,
        UnrestrictedValue = address,
        Data = new DadataSuggestionData
        {
            HouseFiasId = houseFiasId ?? "",
            FiasLevel = fiasLevel
        }
    };
}
