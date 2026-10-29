using Domain.Vetis;

namespace Application.Vetis;

internal static class VetisPaging
{
    private const int DefaultLimit = 50;

    private const int MaxLimit = 200;

    public static int Limit(int requested) =>
        requested <= 0 ? DefaultLimit : Math.Min(requested, MaxLimit);

    public static int Skip(int requested) => Math.Max(0, requested);

    /// <summary>
    /// Читается на одну строку больше страницы: так видно, есть ли продолжение, без отдельного подсчёта.
    /// </summary>
    public static VetisRowsPage<T> Page<T>(List<T> fetched, int skip, int limit) =>
        new()
        {
            Rows = fetched.Take(limit).ToList(),
            Skip = skip,
            HasMore = fetched.Count > limit
        };
}
