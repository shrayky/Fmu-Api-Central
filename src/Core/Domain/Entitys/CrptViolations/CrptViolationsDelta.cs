namespace Domain.Entitys.CrptViolations;

public static class CrptViolationsDelta
{
    /// <summary>
    /// Пишет в документ дневной прирост относительно предыдущего снимка месяца.
    /// </summary>
    public static CrptViolationsDailyEntity Apply(
        CrptViolationsDailyEntity today,
        CrptViolationsDailyEntity? previous)
    {
        if (previous is null)
        {
            today.Violations = Clone(SnapshotOf(today));
            today.PenaltyAmountRub = SnapshotPenaltyOf(today);
            return today;
        }

        var current = SnapshotOf(today);
        var before = SnapshotOf(previous);
        var previousByKey = before.ToDictionary(Key);

        today.Violations = [];
        foreach (var item in current)
        {
            previousByKey.TryGetValue(Key(item), out var last);
            var delta = item.ViolationNumber - (last?.ViolationNumber ?? 0);
            if (delta <= 0)
                continue;

            today.Violations.Add(new CrptViolationItem
            {
                ProductGroup = item.ProductGroup,
                Region = item.Region,
                ViolationNumber = delta,
                ViolationResult = item.ViolationResult,
                ViolationResultName = item.ViolationResultName
            });
        }

        var penaltyDelta = SnapshotPenaltyOf(today) - SnapshotPenaltyOf(previous);
        today.PenaltyAmountRub = penaltyDelta > 0 ? penaltyDelta : 0;
        return today;
    }

    private static List<CrptViolationItem> SnapshotOf(CrptViolationsDailyEntity entity)
        => entity.SnapshotViolations.Count > 0 ? entity.SnapshotViolations : entity.Violations;

    private static decimal SnapshotPenaltyOf(CrptViolationsDailyEntity entity)
        => entity.SnapshotViolations.Count > 0 || entity.SnapshotPenaltyAmountRub > 0
            ? entity.SnapshotPenaltyAmountRub
            : entity.PenaltyAmountRub;

    private static string Key(CrptViolationItem item)
        => $"{item.ProductGroup}|{item.Region}|{item.ViolationResult}";

    private static List<CrptViolationItem> Clone(IEnumerable<CrptViolationItem> items)
        => items.Select(item => new CrptViolationItem
        {
            ProductGroup = item.ProductGroup,
            Region = item.Region,
            ViolationNumber = item.ViolationNumber,
            ViolationResult = item.ViolationResult,
            ViolationResultName = item.ViolationResultName
        }).ToList();
}
