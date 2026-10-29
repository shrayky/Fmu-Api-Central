using System.Globalization;

namespace Domain.Vetis;

public static class IncomingChangesWindow
{
    public const int PageSize = 100;

    // Справка Ветис: длина updateDateInterval не больше 31 дня. 30 суток остаются внутри предела.
    public const int SliceDays = 30;

    /// <summary>
    /// Своя дата площадки, иначе дата организации, иначе конец минус срез. Будущая дата сжимается к концу окна.
    /// </summary>
    public static DateTimeOffset Begin(DateTimeOffset end, string? incomingFrom = null, string? enterpriseFrom = null)
    {
        if (TryStart(enterpriseFrom, end, out var own))
            return own;

        if (TryStart(incomingFrom, end, out var shared))
            return shared;

        return end - TimeSpan.FromDays(SliceDays);
    }

    public static DateTimeOffset StartOf(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    public static IReadOnlyList<(DateTimeOffset Begin, DateTimeOffset End)> Slices(DateTimeOffset begin, DateTimeOffset end)
    {
        if (end < begin)
            (begin, end) = (end, begin);

        if (begin == end)
            return [(begin, end)];

        var span = TimeSpan.FromDays(SliceDays);
        var slices = new List<(DateTimeOffset Begin, DateTimeOffset End)>();
        for (var cursor = begin; cursor < end;)
        {
            var next = cursor + span > end ? end : cursor + span;
            slices.Add((cursor, next));
            cursor = next;
        }

        return slices;
    }

    public static DateTimeOffset? NextFrom(DateTimeOffset? latestUpdate, DateTimeOffset? coveredEnd) =>
        latestUpdate ?? coveredEnd;

    /// <summary>
    /// Короткий остаток уже не из-за длины окна: повторный отказ нужно показать как есть.
    /// </summary>
    public static bool RejectsInterval(string? error, DateTimeOffset begin, DateTimeOffset end)
    {
        if (end - begin <= TimeSpan.FromHours(1) || string.IsNullOrWhiteSpace(error))
            return false;

        return error.Contains("интервал", StringComparison.OrdinalIgnoreCase)
            || error.Contains("interval", StringComparison.OrdinalIgnoreCase)
            || error.Contains("updateDate", StringComparison.OrdinalIgnoreCase)
            || error.Contains("beginDate", StringComparison.OrdinalIgnoreCase);
    }

    public static DateTimeOffset Midpoint(DateTimeOffset begin, DateTimeOffset end) =>
        begin + TimeSpan.FromTicks((end - begin).Ticks / 2);

    private static bool TryStart(string? value, DateTimeOffset end, out DateTimeOffset begin)
    {
        var text = (value ?? "").Trim();
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            var start = StartOf(date);
            begin = start > end ? end : start;
            return true;
        }

        if (VetisDateTime.TryParse(text, out var instant))
        {
            begin = instant > end ? end : instant;
            return true;
        }

        begin = default;
        return false;
    }
}
