namespace Domain.Vetis;

public static class VetisAutoQuench
{
    public static IReadOnlyList<string> DueUuids(
        IEnumerable<VetisIncomingVsd> vsds,
        int delayHours,
        DateTimeOffset now,
        bool requireVehicle)
    {
        var wait = delayHours > 0 ? delayHours : 1;
        return (vsds ?? [])
            .Where(vsd => VetisQuench.CanQuench(vsd.Status))
            .Select(vsd => (Vsd: vsd, Issued: IssuedAt(vsd.IssueDate)))
            .Where(item => item.Issued != null && now >= item.Issued.Value.AddHours(wait))
            .Where(item => !requireVehicle || VetisTransport.HasVehicleNumber(item.Vsd.RawXml))
            .OrderBy(item => item.Issued)
            .ThenBy(item => item.Vsd.Uuid, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Vsd.Uuid)
            .ToList();
    }

    /// <summary>
    /// Дата документа — полночь UTC. Ноль значит даты нет, такой документ идёт после датированных.
    /// </summary>
    public static int CompareDocuments(long leftSortAt, string leftId, long rightSortAt, string rightId)
    {
        var leftDated = leftSortAt > 0;
        var rightDated = rightSortAt > 0;
        if (leftDated != rightDated)
            return leftDated ? -1 : 1;

        var byDate = leftSortAt.CompareTo(rightSortAt);
        return byDate != 0
            ? byDate
            : string.CompareOrdinal(leftId ?? "", rightId ?? "");
    }

    private static DateTimeOffset? IssuedAt(string? issueDate) =>
        VetisDateTime.TryParse(issueDate, out var instant) ? instant : null;
}
