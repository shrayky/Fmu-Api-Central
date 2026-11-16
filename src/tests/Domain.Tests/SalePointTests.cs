using Domain.Entitys.Organization;

namespace Domain.Tests;

public class SalePointTests
{
    /// <summary>
    /// Адреса сравниваются обрезкой пробелов по краям и без учёта регистра.
    /// </summary>
    [Fact]
    public void Две_открытые_точки_с_одинаковым_адресом_не_проходят()
    {
        var points = new List<SalePoint>
        {
            new() { Uid = "p1", Name = "Склад", Address = "Москва, Ленина, 1" },
            new() { Uid = "p2", Name = "Магазин", Address = "  москва, ленина, 1  " }
        };

        var collision = SalePointDirectory.Collision(points);

        Assert.NotNull(collision);
        Assert.Equal("p1", collision.Value.Point.Uid);
        Assert.Equal("  москва, ленина, 1  ", collision.Value.Address);
        Assert.False(collision.Value.Alias);
    }

    [Fact]
    public void Синоним_совпавший_с_адресом_чужой_точки_не_проходит()
    {
        var points = new List<SalePoint>
        {
            new() { Uid = "p1", Name = "Склад", Address = "Москва, Ленина, 1" },
            new()
            {
                Uid = "p2",
                Name = "Магазин",
                Address = "Москва, Ленина, 2",
                Aliases = [new SalePointAlias { Uid = "a1", Address = "МОСКВА, ЛЕНИНА, 1" }]
            }
        };

        var collision = SalePointDirectory.Collision(points);

        Assert.NotNull(collision);
        Assert.Equal("p1", collision.Value.Point.Uid);
        Assert.Equal("МОСКВА, ЛЕНИНА, 1", collision.Value.Address);
        Assert.True(collision.Value.Alias);
    }

    [Fact]
    public void Закрытая_точка_в_проверке_не_участвует()
    {
        var points = new List<SalePoint>
        {
            new() { Uid = "p1", Name = "Старый", Address = "Москва, Ленина, 1", Closed = true },
            new() { Uid = "p2", Name = "Новый", Address = "Москва, Ленина, 1" }
        };

        Assert.Null(SalePointDirectory.Collision(points));
    }

    /// <summary>
    /// Синоним самой точки — не расхождение между двумя точками.
    /// </summary>
    [Fact]
    public void Синоним_совпавший_с_адресом_своей_точки_не_считается_ошибкой()
    {
        var points = new List<SalePoint>
        {
            new()
            {
                Uid = "p1",
                Name = "Склад",
                Address = "Москва, Ленина, 1",
                Aliases = [new SalePointAlias { Uid = "a1", Address = "москва, ленина, 1" }]
            }
        };

        Assert.Null(SalePointDirectory.Collision(points));
    }

    [Fact]
    public void Пустой_адрес_и_пустой_синоним_в_проверке_пропускаются()
    {
        var points = new List<SalePoint>
        {
            new() { Uid = "p1", Name = "Склад", Address = "  ", Aliases = [new SalePointAlias { Address = "" }] },
            new() { Uid = "p2", Name = "Магазин", Address = "" }
        };

        Assert.Null(SalePointDirectory.Collision(points));
    }

    [Fact]
    public void Справочник_находит_точку_по_uid_без_учёта_регистра()
    {
        var organization = new OrganizationEntity
        {
            Id = "o1",
            SalePoints =
            [
                new SalePoint { Uid = "p1", Name = "Склад", Address = "Москва, Ленина, 1" },
                new SalePoint { Uid = "p2", Name = "Старый", Address = "Москва, Ленина, 2", Closed = true }
            ]
        };
        var directory = new SalePointDirectory([organization]);

        Assert.Equal("Склад", directory.Find("o1", "P1")?.Name);
        Assert.True(directory.Closed("o1", "p2"));
        Assert.Null(directory.Find("o2", "p1"));
    }

    [Fact]
    public void Открытые_точки_отдаются_без_закрытых()
    {
        var organization = new OrganizationEntity
        {
            Id = "o1",
            SalePoints =
            [
                new SalePoint { Uid = "p1", Name = "Склад", Address = "Москва, Ленина, 1" },
                new SalePoint { Uid = "p2", Name = "Старый", Address = "Москва, Ленина, 2", Closed = true }
            ]
        };
        var directory = new SalePointDirectory([organization]);

        var open = directory.Options("o1", openOnly: true);

        var point = Assert.Single(open);
        Assert.Equal("p1", point.Uid);
    }
}
