using Domain.Etrn;

namespace Domain.Tests;

public class EtrnAcceptanceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Отказ_без_общего_комментария_отклоняется(string? comment)
    {
        var result = EtrnAcceptance.Decide([Line("Груз", places: 10, weight: "100")], [], comment, EtrnAcceptanceIntent.Reject);

        Assert.True(result.IsFailure);
        Assert.Equal("Для отказа нужен общий комментарий", result.Error);
    }

    [Fact]
    public void Отказ_не_смотрит_на_факты()
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: 10, weight: "100")],
            [new EtrnAcceptanceFact { Index = 1, FactPlaces = 999, FactWeight = "999", Comment = "строка" }],
            "брак",
            EtrnAcceptanceIntent.Reject);

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Rejected, result.Value.Action);
        Assert.Equal(EtrnDocumentStatus.Rejected, result.Value.Status);
        var line = Assert.Single(result.Value.Lines);
        Assert.Null(line.FactPlaces);
        Assert.Equal("", line.FactWeight);
        Assert.Equal("", line.Comment);
    }

    [Fact]
    public void Масса_10_и_10_000_совпадают()
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: 1, weight: "10")],
            [new EtrnAcceptanceFact { Index = 1, FactPlaces = 1, FactWeight = "10.000" }],
            "",
            EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Received, result.Value.Action);
    }

    [Fact]
    public void Комментарий_при_полном_совпадении_не_меняет_действие()
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: 1, weight: "10")],
            [new EtrnAcceptanceFact { Index = 1, FactPlaces = 1, FactWeight = "10", Comment = "всё хорошо" }],
            "общий",
            EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Received, result.Value.Action);
        Assert.Equal(EtrnDocumentStatus.Accepted, result.Value.Status);
        Assert.Equal("", Assert.Single(result.Value.Lines).Comment);
    }

    [Fact]
    public void Факт_0_при_ненулевой_накладной_это_расхождение()
    {
        var lines = new[] { Line("Груз", places: 10, weight: "100") };
        var facts = new[] { new EtrnAcceptanceFact { Index = 1, FactPlaces = 0, FactWeight = "100" } };

        var withoutComment = EtrnAcceptance.Decide(lines, facts, "", EtrnAcceptanceIntent.Sign);
        var withComment = EtrnAcceptance.Decide(lines, facts, "недовоз", EtrnAcceptanceIntent.Sign);

        Assert.True(withoutComment.IsFailure);
        Assert.Equal("При расхождении факта нужен общий комментарий", withoutComment.Error);
        Assert.True(withComment.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Partially, withComment.Value.Action);
        Assert.Equal("недовоз", Assert.Single(withComment.Value.Lines).Comment);
    }

    [Theory]
    [InlineData(-1, "100")]
    [InlineData(11, "100")]
    [InlineData(10, "-1")]
    [InlineData(10, "101")]
    public void Факт_вне_диапазона_отклоняется(int factPlaces, string factWeight)
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: 10, weight: "100")],
            [new EtrnAcceptanceFact { Index = 1, FactPlaces = factPlaces, FactWeight = factWeight }],
            "комментарий",
            EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Строка_без_массы_не_сравнивает_массу()
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: 5, weight: "")],
            [new EtrnAcceptanceFact { Index = 1, FactPlaces = 5, FactWeight = "500" }],
            "",
            EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Received, result.Value.Action);
    }

    [Fact]
    public void Строка_без_мест_не_сравнивает_места()
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: null, weight: "100")],
            [new EtrnAcceptanceFact { Index = 1, FactPlaces = 7, FactWeight = "100" }],
            "",
            EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Received, result.Value.Action);
    }

    [Fact]
    public void Частичная_приёмка_копирует_общий_комментарий_только_в_пустые_строки()
    {
        var lines = new[]
        {
            Line("Груз 1", places: 10, weight: "100", index: 1),
            Line("Груз 2", places: 10, weight: "100", index: 2),
            Line("Груз 3", places: 10, weight: "100", index: 3)
        };
        var facts = new[]
        {
            new EtrnAcceptanceFact { Index = 1, FactPlaces = 9, FactWeight = "100", Comment = "свой" },
            new EtrnAcceptanceFact { Index = 2, FactPlaces = 9, FactWeight = "100" },
            new EtrnAcceptanceFact { Index = 3, FactPlaces = 10, FactWeight = "100" }
        };

        var result = EtrnAcceptance.Decide(lines, facts, "общий", EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnAcceptanceAction.Partially, result.Value.Action);
        Assert.Equal(["свой", "общий", "общий"], result.Value.Lines.Select(line => line.Comment));
    }

    [Fact]
    public void Приёмка_без_строк_отклоняется()
    {
        var result = EtrnAcceptance.Decide([], [], "комментарий", EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Строка_без_мест_и_массы_не_принимается()
    {
        var result = EtrnAcceptance.Decide(
            [Line("Груз", places: null, weight: "")],
            [],
            "комментарий",
            EtrnAcceptanceIntent.Sign);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnDocumentStatus.NoQuantityError, result.Error);
    }

    private static EtrnCargoLine Line(string name, int? places, string weight, int index = 1) => new()
    {
        Index = index,
        Name = name,
        Places = places,
        GrossWeight = weight
    };
}
