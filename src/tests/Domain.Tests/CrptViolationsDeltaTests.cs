using Domain.Entitys.CrptViolations;

namespace Domain.Tests;

public class CrptViolationsDeltaTests
{
    /// <summary>
    /// Первая загрузка месяца: дневной прирост равен всему снимку.
    /// </summary>
    [Fact]
    public void Apply_без_предыдущего_копирует_снимок()
    {
        var today = Entity(
            snapshotPenalty: 1500,
            snapshot: [Item(8, "Красноярский край", "50", 2)]);

        CrptViolationsDelta.Apply(today, previous: null);

        Assert.Single(today.Violations);
        Assert.Equal(2, today.Violations[0].ViolationNumber);
        Assert.Equal(1500, today.PenaltyAmountRub);
        Assert.Equal(2, today.SnapshotViolations[0].ViolationNumber);
        Assert.Equal(1500, today.SnapshotPenaltyAmountRub);
    }

    /// <summary>
    /// Прирост по ключу группа+регион+код; уменьшение не пишем.
    /// </summary>
    [Fact]
    public void Apply_прирост_и_новые_строки()
    {
        var previous = Entity(
            snapshotPenalty: 1000,
            snapshot:
            [
                Item(8, "Красноярский край", "50", 2),
                Item(8, "Красноярский край", "41", 5)
            ]);
        var today = Entity(
            snapshotPenalty: 1800,
            snapshot:
            [
                Item(8, "Красноярский край", "50", 5),
                Item(8, "Красноярский край", "41", 3),
                Item(3, "Москва", "50", 4)
            ]);

        CrptViolationsDelta.Apply(today, previous);

        Assert.Equal(800, today.PenaltyAmountRub);
        Assert.Equal(2, today.Violations.Count);
        Assert.Contains(today.Violations, item => item.ViolationResult == "50" && item.Region == "Красноярский край" && item.ViolationNumber == 3);
        Assert.Contains(today.Violations, item => item.Region == "Москва" && item.ViolationNumber == 4);
        Assert.DoesNotContain(today.Violations, item => item.ViolationResult == "41");
    }

    /// <summary>
    /// Штраф меньше предыдущего снимка обнуляем, отрицательную дельту не храним.
    /// </summary>
    [Fact]
    public void Apply_штраф_не_растёт_ноль()
    {
        var previous = Entity(snapshotPenalty: 2000, snapshot: []);
        var today = Entity(snapshotPenalty: 1500, snapshot: []);

        CrptViolationsDelta.Apply(today, previous);

        Assert.Equal(0, today.PenaltyAmountRub);
        Assert.Empty(today.Violations);
    }

    private static CrptViolationsDailyEntity Entity(decimal snapshotPenalty, CrptViolationItem[] snapshot)
        => new()
        {
            SnapshotPenaltyAmountRub = snapshotPenalty,
            SnapshotViolations = snapshot.ToList(),
            PenaltyAmountRub = 0,
            Violations = []
        };

    private static CrptViolationItem Item(int group, string region, string result, int number)
        => new()
        {
            ProductGroup = group,
            Region = region,
            ViolationResult = result,
            ViolationResultName = "тест",
            ViolationNumber = number
        };
}
