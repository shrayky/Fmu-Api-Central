namespace Domain.Vetis;

public static class VetisIncomingStatus
{
    public const string Accepted = "accepted";

    public const string Partial = "partial";

    public const string NotAccepted = "notAccepted";

    public const string Rejected = "rejected";

    private static readonly string[] Annulled = ["WITHDRAWN", "CANCELED"];

    private static readonly string[] Utilized = ["UTILIZED", "FINALIZED"];

    /// <summary>
    /// Аннулированные ВСД не влияют на результат, пока в документе есть действующие.
    /// </summary>
    public static string Of(IEnumerable<string> vsdStatuses)
    {
        var all = vsdStatuses.ToList();
        var active = all.Where(status => !Matches(Annulled, status)).ToList();
        if (all.Count > 0 && active.Count == 0)
            return Rejected;

        var accepted = active.Count(status => Matches(Utilized, status));
        if (active.Count > 0 && accepted == active.Count)
            return Accepted;

        return accepted > 0 ? Partial : NotAccepted;
    }

    public static bool IsUtilized(string status) => Matches(Utilized, status);

    private static bool Matches(string[] statuses, string status) =>
        statuses.Contains(status ?? "", StringComparer.OrdinalIgnoreCase);
}
