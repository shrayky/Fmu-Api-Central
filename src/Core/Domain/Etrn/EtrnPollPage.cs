namespace Domain.Etrn;

public sealed record EtrnPollPosition(string EventId = "", string DocumentId = "")
{
    public static EtrnPollPosition Empty { get; } = new();

    public bool IsEmpty => EventId.Length == 0 && DocumentId.Length == 0;
}

public sealed record EtrnPollEvent(string EventId, string DocumentId, string Stage = "")
{
    public const string AcceptanceStage = "Приемка груза";

    public bool IsAcceptance =>
        string.Equals((Stage ?? "").Trim(), AcceptanceStage, StringComparison.OrdinalIgnoreCase);
}

public static class EtrnPollPage
{
    /// <summary>
    /// Пара двигается только по разобранному префиксу страницы. Событие, которое не прочиталось, становится парой:
    /// следующий круг начнётся с него, а хвост страницы не запрашивается.
    /// </summary>
    public static EtrnPollPosition Advance(EtrnPollPosition current, IReadOnlyList<EtrnPollEvent>? page, int processed)
    {
        if (page == null || page.Count == 0)
            return current;

        var index = Math.Clamp(processed, 0, page.Count);
        var target = index < page.Count ? page[index] : page[^1];
        return new EtrnPollPosition(target.EventId, target.DocumentId);
    }
}
