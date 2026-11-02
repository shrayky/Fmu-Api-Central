using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Configuration.Options;
using Domain.Entitys.Organization;
using Domain.Vetis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class VetisIncomingQueryServiceTests
{
    [Fact]
    public async Task List_подставляет_названия_организации_и_площадки()
    {
        var world = new World();
        world.AddDocument("d1", "o1", "e1", sortAt: 10);
        world.AddDocument("d2", "o1", "e2", sortAt: 9);
        world.Incoming.Store["d1"].QuenchedBy = "Автогашение";
        world.Incoming.Store["d1"].QuenchedAt = "2026-10-30T12:00:00+07:00";

        var page = (await world.Sut.ListAsync("", "", "", 0, 10)).Value;

        var first = page.Rows.Single(item => item.Id == "d1");
        Assert.Equal("2026-10-29T06:15:00+07:00", first.ReceivedAt);
        Assert.Equal("Автогашение", first.QuenchedBy);
        Assert.Equal("2026-10-30T12:00:00+07:00", first.QuenchedAt);
        Assert.Equal("Колбасный дворик", first.OrganizationName);
        Assert.Equal("Металлургов, 32", first.EnterpriseName);
        Assert.Equal("Магазин-2", page.Rows.Single(item => item.Id == "d2").EnterpriseName);
    }

    [Fact]
    public async Task List_отдаёт_страницу_и_признак_продолжения()
    {
        var world = new World();
        for (var index = 0; index < 5; index++)
            world.AddDocument("d" + index, "o1", "e1", sortAt: 100 - index);

        var first = (await world.Sut.ListAsync("", "", "", 0, 2)).Value;
        var last = (await world.Sut.ListAsync("", "", "", 4, 2)).Value;

        Assert.Equal(["d0", "d1"], first.Rows.Select(item => item.Id));
        Assert.True(first.HasMore);
        Assert.Equal(0, first.Skip);
        Assert.Equal(["d4"], last.Rows.Select(item => item.Id));
        Assert.False(last.HasMore);
        Assert.Equal(4, last.Skip);
    }

    [Fact]
    public async Task List_фильтрует_по_организации_площадке_и_статусу()
    {
        var world = new World();
        world.AddDocument("d1", "o1", "e1", sortAt: 3, status: VetisIncomingStatus.Accepted);
        world.AddDocument("d2", "o1", "e2", sortAt: 2, status: VetisIncomingStatus.Accepted);
        world.AddDocument("d3", "o2", "e9", sortAt: 1, status: VetisIncomingStatus.Rejected);

        Assert.Equal(["d1", "d2"], (await world.Sut.ListAsync("o1", "", "", 0, 10)).Value.Rows.Select(item => item.Id));
        Assert.Equal(["d2"], (await world.Sut.ListAsync("o1", "e2", "", 0, 10)).Value.Rows.Select(item => item.Id));
        Assert.Equal(["d3"], (await world.Sut.ListAsync("", "", VetisIncomingStatus.Rejected, 0, 10)).Value.Rows.Select(item => item.Id));
    }

    [Fact]
    public async Task List_игнорирует_неизвестный_статус()
    {
        var world = new World();
        world.AddDocument("d1", "o1", "e1", sortAt: 1);

        var page = (await world.Sut.ListAsync("", "", "что-то", 0, 10)).Value;

        Assert.Single(page.Rows);
    }

    [Fact]
    public async Task List_ограничивает_размер_страницы()
    {
        var world = new World();
        for (var index = 0; index < 250; index++)
            world.AddDocument("d" + index, "o1", "e1", sortAt: index);

        var huge = (await world.Sut.ListAsync("", "", "", 0, 100000)).Value;
        var empty = (await world.Sut.ListAsync("", "", "", 0, 0)).Value;

        Assert.Equal(200, huge.Rows.Count);
        Assert.Equal(50, empty.Rows.Count);
        Assert.Equal(0, (await world.Sut.ListAsync("", "", "", -5, 1)).Value.Skip);
    }

    [Fact]
    public async Task Get_отдаёт_документ_с_ВСД_и_исходным_xml()
    {
        var world = new World();
        world.AddDocument("d1", "o1", "e1", sortAt: 1);

        var view = (await world.Sut.GetAsync("d1")).Value;

        var vsd = Assert.Single(view.Vsds);
        Assert.Equal("v-d1", vsd.Uuid);
        Assert.Equal("Колбаса", vsd.ProductName);
        Assert.Equal("<vetDocument/>", vsd.RawXml);
        Assert.Equal("", vsd.Transport);
        Assert.Equal("Колбасный дворик", view.OrganizationName);
    }

    [Fact]
    public async Task Get_неизвестного_документа_возвращает_ошибку()
    {
        var result = await new World().Sut.GetAsync("нет");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Filters_отдают_только_организации_с_включённым_Ветис_и_их_площадки()
    {
        var world = new World();
        world.Organizations.Store["o3"] = new OrganizationEntity
        {
            Id = "o3",
            Name = "Без Ветис",
            VetisSettings = new OrganizationVetisSettings { Enable = false }
        };

        var filters = await world.Sut.FiltersAsync();

        var organization = Assert.Single(filters);
        Assert.Equal("o1", organization.Id);
        Assert.Equal(["Металлургов, 32", "Магазин-2"], organization.Enterprises.Select(item => item.Name));
    }

    [Fact]
    public async Task Filters_не_ждут_подсчёт_непринятых()
    {
        var world = PendingDocuments();

        var organization = Assert.Single(await world.Sut.FiltersAsync());

        Assert.Equal(0, organization.NotAcceptedCount);
        Assert.All(organization.Enterprises, item => Assert.Equal(0, item.NotAcceptedCount));
    }

    [Fact]
    public async Task NotAcceptedCounts_отдаёт_ошибку_базы_а_не_нули()
    {
        var world = PendingDocuments();
        world.Incoming.NotAcceptedError = "CouchDB недоступна";

        var result = await world.Sut.NotAcceptedCountsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("CouchDB недоступна", result.Error);
    }

    [Fact]
    public async Task NotAcceptedCounts_считают_непринятые_документы_по_площадкам()
    {
        var world = PendingDocuments();

        var organization = Assert.Single((await world.Sut.NotAcceptedCountsAsync()).Value);

        Assert.Equal(3, organization.NotAcceptedCount);
        Assert.Equal(2, organization.Enterprises.Single(item => item.Guid == "e1").NotAcceptedCount);
        Assert.Equal(1, organization.Enterprises.Single(item => item.Guid == "e2").NotAcceptedCount);
    }

    private static World PendingDocuments()
    {
        var world = new World();
        world.AddDocument("d1", "o1", "e1", sortAt: 3, status: VetisIncomingStatus.NotAccepted);
        world.AddDocument("d2", "o1", "e1", sortAt: 2, status: VetisIncomingStatus.NotAccepted);
        world.AddDocument("d3", "o1", "e1", sortAt: 1, status: VetisIncomingStatus.Partial);
        world.AddDocument("d4", "o1", "e2", sortAt: 1, status: VetisIncomingStatus.NotAccepted);
        return world;
    }

    private sealed class World
    {
        public World()
        {
            Organizations.Store["o1"] = new OrganizationEntity
            {
                Id = "o1",
                Name = "Колбасный дворик",
                VetisSettings = new OrganizationVetisSettings
                {
                    Enable = true,
                    Enterprises =
                    [
                        new OrganizationEnterprise { Guid = "e1", Name = "Колбасный дворик", Presentation = "Металлургов, 32", Participates = true },
                        new OrganizationEnterprise { Guid = "e2", Name = "Магазин-2", Participates = true }
                    ]
                }
            };
            Sut = new VetisIncomingQueryService(Incoming, Organizations);
        }

        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryVetisIncoming Incoming { get; } = new();

        public VetisIncomingQueryService Sut { get; }

        public void AddDocument(string id, string organizationId, string enterpriseGuid, long sortAt, string status = VetisIncomingStatus.NotAccepted)
        {
            Incoming.Store[id] = new VetisIncomingDocumentEntity
            {
                Id = id,
                OrganizationId = organizationId,
                EnterpriseGuid = enterpriseGuid,
                Number = "ТТН-" + id,
                Status = status,
                SortAt = sortAt,
                ReceivedAt = "2026-10-29T06:15:00+07:00",
                VsdCount = 1,
                Vsds = [new VetisIncomingVsd { Uuid = "v-" + id, ProductName = "Колбаса", RawXml = "<vetDocument/>" }]
            };
        }
    }
}

public class VetisApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_отдаёт_строки_с_названиями_и_признаком_ответа_но_без_xml()
    {
        var world = new World();
        world.Add("a1", createdAt: 10, organizationId: "o1", enterpriseGuid: "e1", responseXml: "<r/>");
        world.Add("a2", createdAt: 5, organizationId: "o1", enterpriseGuid: "", responseXml: "");

        var page = (await world.Sut.ListAsync("", "", 0, 10)).Value;

        Assert.Equal(["a1", "a2"], page.Rows.Select(item => item.Id));
        Assert.Equal("Колбасный дворик", page.Rows[0].OrganizationName);
        Assert.Equal("Металлургов, 32", page.Rows[0].EnterpriseName);
        Assert.Equal("", page.Rows[1].EnterpriseName);
        Assert.True(page.Rows[0].HasResponse);
        Assert.False(page.Rows[1].HasResponse);
    }

    [Fact]
    public async Task List_фильтрует_и_листает()
    {
        var world = new World();
        world.Add("a1", createdAt: 3, organizationId: "o1", enterpriseGuid: "e1");
        world.Add("a2", createdAt: 2, organizationId: "o1", enterpriseGuid: "e1");
        world.Add("a3", createdAt: 1, organizationId: "o2", enterpriseGuid: "e9");

        var first = (await world.Sut.ListAsync("o1", "", 0, 1)).Value;
        var onlyOther = (await world.Sut.ListAsync("o2", "e9", 0, 10)).Value;

        Assert.Equal(["a1"], first.Rows.Select(item => item.Id));
        Assert.True(first.HasMore);
        Assert.Equal(["a3"], onlyOther.Rows.Select(item => item.Id));
        Assert.False(onlyOther.HasMore);
    }

    [Fact]
    public async Task Get_отдаёт_запрос_и_ответ_с_отступами()
    {
        var world = new World();
        world.Add("a1", createdAt: 1, organizationId: "o1", enterpriseGuid: "e1", requestXml: "<a><b>1</b></a>", responseXml: "<x><y/></x>");

        var view = (await world.Sut.GetAsync("a1")).Value;

        Assert.Contains("\n", view.RequestXml);
        Assert.Contains("<b>1</b>", view.RequestXml);
        Assert.Contains("\n", view.ResponseXml);
    }

    [Fact]
    public async Task Get_неизвестной_заявки_возвращает_ошибку()
    {
        Assert.True((await new World().Sut.GetAsync("нет")).IsFailure);
    }

    private sealed class World
    {
        public World()
        {
            Organizations.Store["o1"] = new OrganizationEntity
            {
                Id = "o1",
                Name = "Колбасный дворик",
                VetisSettings = new OrganizationVetisSettings
                {
                    Enterprises = [new OrganizationEnterprise { Guid = "e1", Name = "Склад", Presentation = "Металлургов, 32" }]
                }
            };
            Sut = new VetisApplicationService(Applications, Organizations);
        }

        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryVetisApplications Applications { get; } = new();

        public VetisApplicationService Sut { get; }

        public void Add(
            string id,
            long createdAt,
            string organizationId,
            string enterpriseGuid,
            string requestXml = "<request/>",
            string responseXml = "")
        {
            Applications.Store[id] = new VetisApplicationEntity
            {
                Id = id,
                CreatedAt = createdAt,
                OrganizationId = organizationId,
                EnterpriseGuid = enterpriseGuid,
                RequestXml = requestXml,
                ResponseXml = responseXml,
                HasResponse = responseXml.Length > 0,
                Operation = "GetVetDocumentChangesList"
            };
        }
    }
}

public class VetisApplicationCleanupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Clean_удаляет_выполненные_старше_суток_и_оставляет_свежие()
    {
        var world = new World();
        world.Add("old", completed: true, age: TimeSpan.FromHours(25));
        world.Add("fresh", completed: true, age: TimeSpan.FromHours(23));

        var deleted = await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["fresh"], world.Applications.Store.Keys);
    }

    [Fact]
    public async Task Clean_неудачные_заявки_живут_дольше()
    {
        var world = new World();
        world.Add("failed-3d", completed: false, age: TimeSpan.FromDays(3));
        world.Add("failed-8d", completed: false, age: TimeSpan.FromDays(8));

        await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(["failed-3d"], world.Applications.Store.Keys);
    }

    [Fact]
    public async Task Clean_берёт_сроки_из_настроек_сервера()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.ApplicationRetentionDays = 3;
        world.Parameters.Value.VetisSettings.FailedApplicationRetentionDays = 10;
        world.Add("ok-2d", completed: true, age: TimeSpan.FromDays(2));
        world.Add("ok-4d", completed: true, age: TimeSpan.FromDays(4));
        world.Add("bad-8d", completed: false, age: TimeSpan.FromDays(8));

        await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(["bad-8d", "ok-2d"], world.Applications.Store.Keys.OrderBy(item => item));
    }

    [Fact]
    public async Task Clean_удаляет_пачками_пока_есть_устаревшие()
    {
        var world = new World();
        for (var index = 0; index < 450; index++)
            world.Add("old" + index, completed: true, age: TimeSpan.FromDays(2));

        var deleted = await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Equal(450, deleted);
        Assert.Empty(world.Applications.Store);
    }

    [Fact]
    public async Task Clean_работает_и_при_выключенной_интеграции()
    {
        var world = new World(enable: false);
        world.Add("old", completed: true, age: TimeSpan.FromDays(2));

        await world.Sut.CleanAsync(CancellationToken.None);

        Assert.Empty(world.Applications.Store);
    }

    private sealed class World
    {
        public World(bool enable = true)
        {
            Parameters = new MemoryVetisParameters(enable);
            Sut = new VetisApplicationCleanupService(
                Applications,
                Parameters,
                NullLogger<VetisApplicationCleanupService>.Instance,
                new FixedClock(Now));
        }

        public MemoryVetisParameters Parameters { get; }

        public MemoryVetisApplications Applications { get; } = new();

        public VetisApplicationCleanupService Sut { get; }

        public void Add(string id, bool completed, TimeSpan age)
        {
            Applications.Store[id] = new VetisApplicationEntity
            {
                Id = id,
                Completed = completed,
                CreatedAt = (Now - age).ToUnixTimeSeconds()
            };
        }
    }
}
