using Domain.Entitys.Organization;

namespace Domain.Tests;

public class SalePointMatchTests
{
    private static readonly SalePoint Warehouse = new()
    {
        Uid = "p1",
        Name = "Склад",
        Address = "Москва, Ленина, 1"
    };

    private static readonly SalePoint Shop = new()
    {
        Uid = "p2",
        Name = "Магазин",
        Address = "Москва, Ленина, 2",
        Aliases = [new SalePointAlias { Uid = "a1", Address = "Мск, Ленинский 2" }]
    };

    [Fact]
    public void Адрес_открытой_точки_даёт_её_uid()
    {
        Assert.Equal("p1", SalePointMatch.Find([Warehouse, Shop], "  москва, ленина, 1  "));
    }

    [Fact]
    public void Синоним_даёт_uid_владельца()
    {
        Assert.Equal("p2", SalePointMatch.Find([Warehouse, Shop], "мск, ленинский 2"));
    }

    /// <summary>
    /// Адрес важнее синонима: иначе синоним одной точки перебивал бы точный адрес другой.
    /// </summary>
    [Fact]
    public void Адрес_важнее_синонима()
    {
        var warehouseWithAlias = new SalePoint
        {
            Uid = "p1",
            Name = "Склад",
            Address = "Москва, Ленина, 1",
            Aliases = [new SalePointAlias { Uid = "a1", Address = "Москва, Ленина, 2" }]
        };

        Assert.Equal("p2", SalePointMatch.Find([warehouseWithAlias, Shop], "Москва, Ленина, 2"));
    }

    [Fact]
    public void Два_совпадения_по_адресу_дают_пусто()
    {
        var twin = new SalePoint { Uid = "p3", Name = "Дубль", Address = "Москва, Ленина, 1" };

        Assert.Equal("", SalePointMatch.Find([Warehouse, twin], "Москва, Ленина, 1"));
    }

    [Fact]
    public void Закрытая_точка_не_участвует()
    {
        var closed = new SalePoint { Uid = "p3", Name = "Старый", Address = "Москва, Ленина, 3", Closed = true };

        Assert.Equal("", SalePointMatch.Find([closed], "Москва, Ленина, 3"));
    }

    [Fact]
    public void Пустой_адрес_даёт_пусто()
    {
        Assert.Equal("", SalePointMatch.Find([Warehouse, Shop], "   "));
        Assert.Equal("", SalePointMatch.Find(null, "Москва, Ленина, 1"));
    }
}
