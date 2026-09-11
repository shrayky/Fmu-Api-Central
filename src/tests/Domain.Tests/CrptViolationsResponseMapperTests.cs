using Domain.Entitys.CrptViolations;

namespace Domain.Tests;

public class CrptViolationsResponseMapperTests
{
    /// <summary>
    /// Ответ ЧЗ разворачивается в строки отклонений и сумму штрафа за день.
    /// </summary>
    [Fact]
    public void Map_разворачивает_отклонения_и_штраф()
    {
        const string violationsJson = """
            {
                "analytical_violations_total_by_inn": [
                    {
                        "product_group": 8,
                        "violations": [
                            {
                                "region": "Красноярский край",
                                "violation_number": 2,
                                "violation_result": "50",
                                "violation_result_name": "Реализация товаров с истекшим сроком годности в объемных показателях"
                            },
                            {
                                "region": "Красноярский край",
                                "violation_number": 2,
                                "violation_result": "41",
                                "violation_result_name": "Отсутствуют сведения о нанесении кода"
                            }
                        ]
                    }
                ]
            }
            """;
        const string penaltyJson = """{"analytical_violations_auto_penalty":[{"amount_rub":1500}]}""";
        var date = new DateOnly(2026, 9, 1);

        var entity = CrptViolationsResponseMapper.Map("246412218294", date, violationsJson, penaltyJson);

        Assert.Equal("246412218294_20260901", entity.Id);
        Assert.Equal("246412218294", entity.Inn);
        Assert.Equal(1500, entity.SnapshotPenaltyAmountRub);
        Assert.Equal(1500, entity.PenaltyAmountRub);
        Assert.Equal(2, entity.SnapshotViolations.Count);
        Assert.Equal(2, entity.Violations.Count);
        Assert.Equal(8, entity.Violations[0].ProductGroup);
        Assert.Equal("Красноярский край", entity.Violations[0].Region);
        Assert.Equal(2, entity.Violations[0].ViolationNumber);
        Assert.Equal("50", entity.Violations[0].ViolationResult);
        Assert.Equal("Реализация товаров с истекшим сроком годности в объемных показателях", entity.Violations[0].ViolationResultName);
        Assert.Equal("41", entity.Violations[1].ViolationResult);
    }

    /// <summary>
    /// Пустой ответ ЧЗ даёт документ дня без строк и с нулевым штрафом.
    /// </summary>
    [Fact]
    public void Map_пустой_ответ_даёт_пустой_документ()
    {
        var entity = CrptViolationsResponseMapper.Map(
            "246412218294",
            new DateOnly(2026, 9, 1),
            "{}",
            "{}");

        Assert.Empty(entity.SnapshotViolations);
        Assert.Empty(entity.Violations);
        Assert.Equal(0, entity.SnapshotPenaltyAmountRub);
        Assert.Equal(0, entity.PenaltyAmountRub);
    }
}
