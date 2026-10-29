using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Entitys.Organization;
using Domain.Vetis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class VetisIncomingLoadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Load_ничего_не_делает_когда_Ветис_выключен()
    {
        var world = new World(enable: false);

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Empty(world.Gateway.Requests);
        Assert.Empty(world.Applications.Store);
    }

    [Fact]
    public async Task Load_складывает_ВСД_в_документы_по_накладным_и_пишет_заявку()
    {
        var world = new World();
        world.SkipEnterprise("e2");
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(
            3,
            Vsd("v1", "CONFIRMED", "ТТН-1"),
            Vsd("v2", "UTILIZED", "ТТН-1"),
            Vsd("v3", "CONFIRMED", "ТТН-2")));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal(2, world.Incoming.Store.Count);
        var first = world.Incoming.Store.Values.Single(item => item.Number == "ТТН-1");
        Assert.Equal(2, first.Vsds.Count);
        Assert.Equal("o1", first.OrganizationId);
        Assert.Equal("e1", first.EnterpriseGuid);
        var application = Assert.Single(world.Applications.Store.Values);
        Assert.Equal("GetVetDocumentChangesList", application.Operation);
        Assert.Equal("o1", application.OrganizationId);
        Assert.Equal("e1", application.EnterpriseGuid);
        Assert.True(application.Completed);
        Assert.Equal(Now.ToUnixTimeSeconds(), application.CreatedAt);
    }

    [Fact]
    public async Task Load_дополняет_уже_сохранённый_документ()
    {
        var world = new World();
        var stored = VetisIncomingDocumentEntity.Group("o1", "e1", [Vsd("v1", "CONFIRMED", "ТТН-1")]).Single();
        world.Incoming.Store[stored.Id] = stored;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(1, Vsd("v1", "UTILIZED", "ТТН-1")));

        await world.Sut.LoadAsync(CancellationToken.None);

        var document = Assert.Single(world.Incoming.Store.Values);
        Assert.Equal(VetisIncomingStatus.Accepted, document.Status);
        Assert.Single(document.Vsds);
    }

    [Fact]
    public async Task Load_сдвигает_дату_площадки_на_последнее_обновление_ВСД()
    {
        var world = new World();
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(
            2,
            Vsd("v1", "CONFIRMED", "ТТН-1", updatedAt: "2026-10-20T08:00:00+07:00"),
            Vsd("v2", "CONFIRMED", "ТТН-1", updatedAt: "2026-10-25T09:30:00+07:00")));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal("2026-10-25T09:30:00+07:00", world.Cursor("e1"));
    }

    [Fact]
    public async Task Load_без_ВСД_сдвигает_дату_площадки_на_конец_окна()
    {
        var world = new World();

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.True(VetisDateTime.TryParse(world.Cursor("e1"), out var cursor));
        Assert.Equal(Now, cursor);
    }

    [Fact]
    public async Task Load_не_трогает_другие_площадки_при_сдвиге_даты()
    {
        var world = new World();
        world.SkipEnterprise("e2");

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal("2026-09-01", world.Cursor("e2"));
    }

    [Fact]
    public async Task Load_не_затирает_правки_карточки_сделанные_во_время_загрузки()
    {
        var world = new World();
        world.Gateway.OnIncoming = _ => world.Organizations.Store["o1"].Name = "Новое название";

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal("Новое название", world.Organizations.Store["o1"].Name);
        Assert.Equal(Now, ParseCursor(world.Cursor("e1")));
    }

    [Fact]
    public async Task Load_запрашивает_окно_с_даты_площадки_и_размером_страницы_из_настроек()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.IncomingFetchPageSize = 50;
        world.SetCursor("e1", "2026-10-20");

        await world.Sut.LoadAsync(CancellationToken.None);

        var request = world.Gateway.Requests.First(item => item.EnterpriseGuid == "e1");
        Assert.Equal(IncomingChangesWindow.StartOf(new DateOnly(2026, 10, 20)), request.Begin);
        Assert.Equal(Now, request.End);
        Assert.Equal(50, request.Count);
        Assert.Equal(0, request.Offset);
    }

    [Fact]
    public async Task Load_без_даты_площадки_и_организации_берёт_последние_30_суток()
    {
        var world = new World();
        world.SetCursor("e1", "");
        world.SetOrganizationFrom("");

        await world.Sut.LoadAsync(CancellationToken.None);

        var request = world.Gateway.Requests.First(item => item.EnterpriseGuid == "e1");
        Assert.Equal(Now - TimeSpan.FromDays(30), request.Begin);
    }

    [Fact]
    public async Task Load_берёт_общую_дату_организации_когда_у_площадки_её_нет()
    {
        var world = new World();
        world.SetCursor("e1", "");
        world.SetOrganizationFrom("2026-10-10");

        await world.Sut.LoadAsync(CancellationToken.None);

        var request = world.Gateway.Requests.First(item => item.EnterpriseGuid == "e1");
        Assert.Equal(IncomingChangesWindow.StartOf(new DateOnly(2026, 10, 10)), request.Begin);
    }

    [Fact]
    public async Task Load_подгружает_страницы_пока_страница_полная()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.IncomingFetchPageSize = 2;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(2, Vsd("v1", "CONFIRMED", "ТТН-1"), Vsd("v2", "CONFIRMED", "ТТН-1")));
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(1, Vsd("v3", "CONFIRMED", "ТТН-1")));

        await world.Sut.LoadAsync(CancellationToken.None);

        var requests = world.Gateway.Requests.Where(item => item.EnterpriseGuid == "e1").ToList();
        Assert.Equal([0, 2], requests.Select(item => item.Offset));
        Assert.Equal(3, world.Incoming.Store.Values.Single().Vsds.Count);
    }

    [Fact]
    public async Task Load_не_сдвигает_курсор_когда_страницы_среза_не_исчерпаны()
    {
        var world = new World();
        world.SkipEnterprise("e2");
        world.Parameters.Value.VetisSettings.IncomingFetchPageSize = 1;
        world.SetCursor("e1", "2026-10-01T09:30:00+07:00");
        for (var page = 0; page < 500; page++)
            world.Gateway.Enqueue(ScriptedVetisGateway.Page(1, Vsd("v" + page, "CONFIRMED", "ТТН-1", "2026-10-20T10:00:00+07:00")));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal(500, world.Gateway.Requests.Count(item => item.EnterpriseGuid == "e1"));
        Assert.Equal("2026-10-01T09:30:00+07:00", world.Cursor("e1"));
    }

    [Fact]
    public async Task Load_убирает_ВСД_из_прежнего_документа_когда_сменилась_накладная()
    {
        var world = new World();
        world.SkipEnterprise("e2");
        var previous = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "CONFIRMED", "ТТН-1"),
            Vsd("stay", "CONFIRMED", "ТТН-1")
        ]).Single();
        world.Incoming.Store[previous.Id] = previous;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(1, Vsd("v1", "CONFIRMED", "ТТН-2")));

        await world.Sut.LoadAsync(CancellationToken.None);

        var holders = world.Incoming.Store.Values.Where(document => document.Vsds.Any(item => item.Uuid == "v1")).ToList();
        var moved = Assert.Single(holders);
        Assert.Equal("ТТН-2", moved.Number);
        Assert.Equal(["stay"], world.Incoming.Store[previous.Id].Vsds.Select(item => item.Uuid));
    }

    [Fact]
    public async Task Load_снимает_переехавший_ВСД_из_накладной_той_же_страницы()
    {
        var world = new World();
        world.SkipEnterprise("e2");
        var previous = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "CONFIRMED", "ТТН-1"),
            Vsd("stay", "CONFIRMED", "ТТН-1")
        ]).Single();
        world.Incoming.Store[previous.Id] = previous;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(
            2,
            Vsd("v1", "CONFIRMED", "ТТН-2"),
            Vsd("stay", "CONFIRMED", "ТТН-1")));

        await world.Sut.LoadAsync(CancellationToken.None);

        var holders = world.Incoming.Store.Values.Where(document => document.Vsds.Any(item => item.Uuid == "v1")).ToList();
        var moved = Assert.Single(holders);
        Assert.Equal("ТТН-2", moved.Number);
        Assert.Equal(["stay"], world.Incoming.Store[previous.Id].Vsds.Select(item => item.Uuid));
    }

    [Fact]
    public async Task Load_снимает_uuid_один_раз_на_страницу_ВетИС()
    {
        var world = new World();
        world.SkipEnterprise("e2");
        var first = VetisIncomingDocumentEntity.Group("o1", "e1", [Vsd("v1", "CONFIRMED", "ТТН-1"), Vsd("stay", "CONFIRMED", "ТТН-1")]).Single();
        var second = VetisIncomingDocumentEntity.Group("o1", "e1", [Vsd("v2", "CONFIRMED", "ТТН-3")]).Single();
        world.Incoming.Store[first.Id] = first;
        world.Incoming.Store[second.Id] = second;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(2, Vsd("v1", "CONFIRMED", "ТТН-2"), Vsd("v2", "CONFIRMED", "ТТН-4")));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal(1, world.Incoming.ReleaseCalls);
        Assert.Equal(["stay"], world.Incoming.Store[first.Id].Vsds.Select(item => item.Uuid));
        Assert.DoesNotContain(world.Incoming.Store.Values, document => document.Id == second.Id);
    }

    [Fact]
    public async Task Load_полная_страница_из_исходящих_тоже_ведёт_к_следующей()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.IncomingFetchPageSize = 2;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(2));
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(0));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal(2, world.Gateway.Requests.Count(item => item.EnterpriseGuid == "e1"));
    }

    [Fact]
    public async Task Load_режет_длинный_период_на_срезы()
    {
        var world = new World();
        world.SetCursor("e1", Now.AddDays(-75).ToString("yyyy-MM-ddTHH:mm:ssK"));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal(3, world.Gateway.Requests.Count(item => item.EnterpriseGuid == "e1"));
    }

    [Fact]
    public async Task Load_делит_окно_пополам_когда_Ветис_отклонил_длину_интервала()
    {
        var world = new World();
        world.SetCursor("e1", "2026-10-20");
        world.Gateway.Enqueue(ScriptedVetisGateway.Failed("Недопустимый интервал дат"));

        await world.Sut.LoadAsync(CancellationToken.None);

        var requests = world.Gateway.Requests.Where(item => item.EnterpriseGuid == "e1").ToList();
        Assert.Equal(3, requests.Count);
        Assert.Equal(requests[1].End, requests[2].Begin);
        Assert.Equal(requests[0].Begin, requests[1].Begin);
        Assert.Equal(requests[0].End, requests[2].End);
        Assert.Equal(Now, ParseCursor(world.Cursor("e1")));
    }

    [Fact]
    public async Task Load_не_сдвигает_дату_когда_заявка_не_завершена_и_пишет_неудачную_заявку()
    {
        var world = new World();
        world.Gateway.Enqueue(ScriptedVetisGateway.Failed("Нет доступа"));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal("2026-10-01", world.Cursor("e1"));
        var application = Assert.Single(world.Applications.Store.Values.Where(item => item.EnterpriseGuid == "e1"));
        Assert.False(application.Completed);
        Assert.Equal("Нет доступа", application.Error);
    }

    [Fact]
    public async Task Load_не_сдвигает_дату_когда_документы_не_сохранились()
    {
        var world = new World();
        world.Incoming.FailSave = true;
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(1, Vsd("v1", "CONFIRMED", "ТТН-1", updatedAt: "2026-10-25T09:30:00+07:00")));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal("2026-10-01", world.Cursor("e1"));
    }

    [Fact]
    public async Task Load_ошибка_одной_площадки_не_мешает_другой()
    {
        var world = new World();
        world.Gateway.Enqueue(ScriptedVetisGateway.Failed("Нет доступа"));
        world.Gateway.Enqueue(ScriptedVetisGateway.Page(1, Vsd("v9", "CONFIRMED", "ТТН-9", updatedAt: "2026-10-26T09:00:00+07:00")));

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Equal("2026-10-01", world.Cursor("e1"));
        Assert.Equal("2026-10-26T09:00:00+07:00", world.Cursor("e2"));
        Assert.Contains(world.Incoming.Store.Values, item => item.EnterpriseGuid == "e2");
    }

    [Fact]
    public async Task Load_берёт_только_площадки_с_галкой()
    {
        var world = new World();
        world.Organizations.Store["o1"].VetisSettings.Enterprises.Single(item => item.Guid == "e2").Participates = false;

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.DoesNotContain(world.Gateway.Requests, item => item.EnterpriseGuid == "e2");
    }

    [Fact]
    public async Task Load_пропускает_организацию_с_выключенным_Ветис_и_с_неполными_настройками()
    {
        var world = new World();
        world.Organizations.Store["o1"].VetisSettings.Enable = false;
        world.Organizations.Store["o2"] = Organization("o2");
        world.Organizations.Store["o2"].VetisSettings.ApiKey = "";

        await world.Sut.LoadAsync(CancellationToken.None);

        Assert.Empty(world.Gateway.Requests);
    }

    [Fact]
    public async Task Load_передаёт_в_шлюз_настройки_организации_и_сервера()
    {
        var world = new World();

        await world.Sut.LoadAsync(CancellationToken.None);

        var call = world.Gateway.LastCall;
        Assert.NotNull(call);
        Assert.Equal("key-o1", call.ApiKey);
        Assert.Equal("user-o1", call.BasicLogin);
        Assert.Equal("secret-o1", call.BasicPassword);
        Assert.Equal("issuer-o1", call.IssuerId);
        Assert.Equal("operator-o1", call.InitiatorLogin);
        Assert.Equal(Domain.Vetis.VetisEndpoints.ProductionApplicationManagement, call.Endpoint);
    }

    private static DateTimeOffset ParseCursor(string value)
    {
        Assert.True(VetisDateTime.TryParse(value, out var instant));
        return instant;
    }

    private static VetisIncomingVsd Vsd(string uuid, string status, string number, string updatedAt = "") =>
        new()
        {
            Uuid = uuid,
            Status = status,
            SourceNumber = number,
            SourceDate = "2026-10-01",
            SourceType = "1",
            IssueDate = "2026-10-01T08:00:00+07:00",
            ConsignorName = "Поставщик",
            UpdatedAt = updatedAt
        };

    private static OrganizationEntity Organization(string id) =>
        new()
        {
            Id = id,
            Name = "Организация " + id,
            VetisSettings = new OrganizationVetisSettings
            {
                Enable = true,
                ApiKey = "key-" + id,
                BasicLogin = "user-" + id,
                BasicPassword = "secret-" + id,
                IssuerId = "issuer-" + id,
                InitiatorLogin = "operator-" + id,
                IncomingFrom = "2026-08-01",
                Enterprises =
                [
                    new OrganizationEnterprise { Guid = "e1", Name = "Склад", IncomingFrom = "2026-10-01", Participates = true },
                    new OrganizationEnterprise { Guid = "e2", Name = "Магазин", IncomingFrom = "2026-09-01", Participates = true }
                ]
            }
        };

    private sealed class World
    {
        public World(bool enable = true)
        {
            Parameters = new MemoryVetisParameters(enable);
            Organizations.Store["o1"] = Organization("o1");
            var recorder = new VetisApplicationRecorder(Applications, NullLogger<VetisApplicationRecorder>.Instance, Clock);
            Sut = new VetisIncomingLoadService(
                Gateway,
                Organizations,
                Incoming,
                recorder,
                Parameters,
                NullLogger<VetisIncomingLoadService>.Instance,
                Clock);
        }

        public FixedClock Clock { get; } = new(Now);

        public MemoryVetisParameters Parameters { get; }

        public ScriptedVetisGateway Gateway { get; } = new();

        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryVetisIncoming Incoming { get; } = new();

        public MemoryVetisApplications Applications { get; } = new();

        public VetisIncomingLoadService Sut { get; }

        public string Cursor(string guid) =>
            Organizations.Store["o1"].VetisSettings.Enterprises.Single(item => item.Guid == guid).IncomingFrom;

        public void SetCursor(string guid, string value) =>
            Organizations.Store["o1"].VetisSettings.Enterprises.Single(item => item.Guid == guid).IncomingFrom = value;

        public void SkipEnterprise(string guid) =>
            Organizations.Store["o1"].VetisSettings.Enterprises.Single(item => item.Guid == guid).Participates = false;

        public void SetOrganizationFrom(string value) => Organizations.Store["o1"].VetisSettings.IncomingFrom = value;
    }
}

