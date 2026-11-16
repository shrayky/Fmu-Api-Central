using Application.Etrn;
using Application.SalePoints;
using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Vetis;

namespace Application.Tests;

public class SalePointServiceTests
{
    [Fact]
    public async Task AssignEtrn_пишет_точку_и_возвращает_карточку()
    {
        var world = new World();
        world.AddEtrn("d1");

        var result = await world.Sut.AssignEtrnAsync("d1", "p1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("p1", world.Etrn.Store["d1"].SalePointUid);
        Assert.Equal("Склад", result.Value.SalePointName);
        Assert.False(result.Value.SalePointClosed);
    }

    [Fact]
    public async Task AssignEtrn_пустая_строка_очищает_точку()
    {
        var world = new World();
        world.AddEtrn("d1", salePointUid: "p1");

        var result = await world.Sut.AssignEtrnAsync("d1", "");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("", world.Etrn.Store["d1"].SalePointUid);
        Assert.Equal("", result.Value.SalePointName);
    }

    [Fact]
    public async Task AssignEtrn_неизвестная_точка_даёт_ошибку()
    {
        var world = new World();
        world.AddEtrn("d1");

        var result = await world.Sut.AssignEtrnAsync("d1", "нет");

        Assert.True(result.IsFailure);
        Assert.Equal("Точка не найдена в справочнике организации", result.Error);
        Assert.Equal("", world.Etrn.Store["d1"].SalePointUid);
    }

    [Fact]
    public async Task AssignEtrn_закрытая_точка_даёт_ошибку()
    {
        var world = new World();
        world.AddEtrn("d1");

        var result = await world.Sut.AssignEtrnAsync("d1", "p2");

        Assert.True(result.IsFailure);
        Assert.Equal("Точка закрыта: выберите открытую", result.Error);
    }

    [Fact]
    public async Task AssignVetis_пишет_точку_и_возвращает_карточку()
    {
        var world = new World();
        world.AddVetis("d1");

        var result = await world.Sut.AssignVetisAsync("d1", "p1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("p1", world.Vetis.Store["d1"].SalePointUid);
        Assert.Equal("Склад", result.Value.SalePointName);
    }

    [Fact]
    public async Task AssignVetis_неизвестный_документ_даёт_ошибку()
    {
        var world = new World();

        var result = await world.Sut.AssignVetisAsync("нет", "p1");

        Assert.True(result.IsFailure);
        Assert.Equal("Документ не найден.", result.Error);
    }

    [Fact]
    public async Task UsedAsync_ищет_ссылку_в_обоих_журналах()
    {
        var world = new World();
        world.AddVetis("v1", salePointUid: "p1");

        Assert.True((await world.Usage.UsedAsync("o1", "p1")).Value);
        Assert.False((await world.Usage.UsedAsync("o1", "p2")).Value);
    }

    /// <summary>
    /// Подтверждённое добавление: адрес документа записывается синонимом выбранной точки.
    /// </summary>
    [Fact]
    public async Task RememberEtrnAlias_записывает_адрес_документа_синонимом()
    {
        var world = new World();
        world.AddEtrn("d1", address: "Казань, Баумана, 1");
        await world.Sut.AssignEtrnAsync("d1", "p1");

        var result = await world.Sut.RememberEtrnAliasAsync("d1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        var point = world.Organizations.Store["o1"].SalePoints.Single(item => item.Uid == "p1");
        var alias = Assert.Single(point.Aliases);
        Assert.Equal("Казань, Баумана, 1", alias.Address);
        Assert.False(string.IsNullOrWhiteSpace(alias.Uid));
    }

    [Fact]
    public async Task RememberEtrnAlias_не_дублирует_совпавший_адрес()
    {
        var world = new World();
        world.AddEtrn("d1", address: " москва, ленина, 1 ");
        await world.Sut.AssignEtrnAsync("d1", "p1");

        var result = await world.Sut.RememberEtrnAliasAsync("d1");

        Assert.True(result.IsFailure);
        Assert.Equal("Адрес уже указан у точки «Склад»", result.Error);
        var point = world.Organizations.Store["o1"].SalePoints.Single(item => item.Uid == "p1");
        Assert.Empty(point.Aliases);
    }

    [Fact]
    public async Task RememberEtrnAlias_не_запоминает_адрес_чужой_точки()
    {
        var world = new World();
        world.Organizations.Store["o1"].SalePoints.Add(new SalePoint
        {
            Uid = "p3",
            Name = "Казанский",
            Address = "Казань, Баумана, 1"
        });
        world.AddEtrn("d1", address: "Казань, Баумана, 1");
        await world.Sut.AssignEtrnAsync("d1", "p1");

        var result = await world.Sut.RememberEtrnAliasAsync("d1");

        Assert.True(result.IsFailure);
        Assert.Equal("Адрес «Казань, Баумана, 1» уже указан у открытой точки «Казанский»", result.Error);
        var point = world.Organizations.Store["o1"].SalePoints.Single(item => item.Uid == "p1");
        Assert.Empty(point.Aliases);
    }

    [Fact]
    public async Task RememberEtrnAlias_без_выбранной_точки_ничего_не_делает()
    {
        var world = new World();
        world.AddEtrn("d1", address: "Казань, Баумана, 1");

        var result = await world.Sut.RememberEtrnAliasAsync("d1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        var point = world.Organizations.Store["o1"].SalePoints.Single(item => item.Uid == "p1");
        Assert.Empty(point.Aliases);
    }

    [Fact]
    public async Task RememberEtrnAlias_без_адреса_сообщает_ошибку()
    {
        var world = new World();
        world.AddEtrn("d1");
        await world.Sut.AssignEtrnAsync("d1", "p1");

        var result = await world.Sut.RememberEtrnAliasAsync("d1");

        Assert.True(result.IsFailure);
        Assert.Equal("У документа нет адреса", result.Error);
    }

    [Fact]
    public async Task RememberEtrnAlias_ошибку_записи_организации_отдаёт_наружу()
    {
        var world = new World();
        world.AddEtrn("d1", address: "Казань, Баумана, 1");
        await world.Sut.AssignEtrnAsync("d1", "p1");
        world.Organizations.UpdateError = "Конфликт ревизии";

        var result = await world.Sut.RememberEtrnAliasAsync("d1");

        Assert.True(result.IsFailure);
        Assert.Equal("Конфликт ревизии", result.Error);
        Assert.Empty(world.Organizations.Store["o1"].SalePoints.Single(item => item.Uid == "p1").Aliases);
    }

    [Fact]
    public async Task RememberVetisAlias_записывает_адрес_площадки_синонимом()
    {
        var world = new World();
        world.Organizations.Store["o1"].VetisSettings.Enterprises[0].Address = "Казань, Баумана, 1";
        world.AddVetis("v1");
        await world.Sut.AssignVetisAsync("v1", "p1");

        var result = await world.Sut.RememberVetisAliasAsync("v1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        var point = world.Organizations.Store["o1"].SalePoints.Single(item => item.Uid == "p1");
        Assert.Equal("Казань, Баумана, 1", Assert.Single(point.Aliases).Address);
    }

    [Fact]
    public async Task MatchEtrn_заполняет_точку_по_адресу()
    {
        var world = new World();
        world.AddEtrn("d1", address: " москва, ленина, 1 ");

        var result = await world.Sut.MatchEtrnAsync("d1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("p1", world.Etrn.Store["d1"].SalePointUid);
        Assert.Equal("Склад", result.Value.SalePointName);
    }

    [Fact]
    public async Task MatchEtrn_не_меняет_выбранную_точку()
    {
        var world = new World();
        world.AddEtrn("d1", salePointUid: "p2", address: "Москва, Ленина, 1");

        var result = await world.Sut.MatchEtrnAsync("d1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("p2", world.Etrn.Store["d1"].SalePointUid);
    }

    [Fact]
    public async Task MatchEtrn_без_совпадения_оставляет_поле_пустым()
    {
        var world = new World();
        world.AddEtrn("d1", address: "Казань, Баумана, 1");

        var result = await world.Sut.MatchEtrnAsync("d1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("", world.Etrn.Store["d1"].SalePointUid);
    }

    [Fact]
    public async Task MatchVetis_берёт_адрес_площадки()
    {
        var world = new World();
        world.AddVetis("v1", enterpriseGuid: "e1");

        var result = await world.Sut.MatchVetisAsync("v1");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("p1", world.Vetis.Store["v1"].SalePointUid);
        Assert.Equal("Склад", result.Value.SalePointName);
    }

    private sealed class World
    {
        public World()
        {
            Organizations.Store["o1"] = new OrganizationEntity
            {
                Id = "o1",
                Name = "ИП",
                SalePoints =
                [
                    new SalePoint { Uid = "p1", Name = "Склад", Address = "Москва, Ленина, 1" },
                    new SalePoint { Uid = "p2", Name = "Старый", Address = "Москва, Ленина, 2", Closed = true }
                ],
                VetisSettings = new OrganizationVetisSettings
                {
                    Enterprises =
                    [
                        new OrganizationEnterprise { Guid = "e1", Name = "Склад", Address = "Москва, Ленина, 1" }
                    ]
                }
            };
            Sut = new SalePointService(
                Organizations,
                Etrn,
                Vetis,
                new EtrnIncomingQueryService(Etrn, Organizations, Sync),
                new VetisIncomingQueryService(Vetis, Organizations));
            Usage = new SalePointUsageService(Etrn, Vetis);
        }

        public SalePointUsageService Usage { get; }

        public MemoryEtrnIncoming Etrn { get; } = new();

        public MemoryVetisIncoming Vetis { get; } = new();

        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryEtrnSyncStates Sync { get; } = new();

        public SalePointService Sut { get; }

        public void AddEtrn(string id, string salePointUid = "", string address = "")
        {
            Etrn.Store[id] = new EtrnIncomingDocumentEntity
            {
                Id = id,
                OrganizationId = "o1",
                Number = "ТН-" + id,
                Status = EtrnDocumentStatus.Waiting,
                SalePointUid = salePointUid,
                DeliveryAddress = address,
                Operator = new EtrnOperator { ConnectionId = "c1", Type = EtrnOperatorType.Saby, Name = "сбис" }
            };
        }

        public void AddVetis(string id, string salePointUid = "", string enterpriseGuid = "e1")
        {
            Vetis.Store[id] = new VetisIncomingDocumentEntity
            {
                Id = id,
                OrganizationId = "o1",
                EnterpriseGuid = enterpriseGuid,
                Number = "ТТН-" + id,
                SalePointUid = salePointUid,
                Vsds = [new VetisIncomingVsd { Uuid = "v-" + id }]
            };
        }
    }
}
