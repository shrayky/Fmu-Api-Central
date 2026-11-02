using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Entitys.Organization;
using Domain.Vetis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class VetisIncomingAutoQuenchServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public async Task Пакет_гасит_документ_целиком_и_не_берёт_следующий_сверх_лимита()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.AutoQuenchBatchSize = 1;
        world.Incoming.Store["d-old"] = Document("d-old", "o1", "2026-10-01",
            Vsd("old-1", "2026-10-01T08:00:00+07:00"),
            Vsd("old-2", "2026-10-01T09:00:00+07:00"),
            Vsd("old-3", "2026-10-01T10:00:00+07:00"));
        world.Incoming.Store["d-mid"] = Document("d-mid", "o1", "2026-10-15", Vsd("mid", "2026-10-15T08:00:00+07:00"));
        world.Gateway.EnqueueQuench(Done("old-1"));
        world.Gateway.EnqueueQuench(Done("old-2"));
        world.Gateway.EnqueueQuench(Done("old-3"));

        var taken = (await world.Sut.QuenchBatchAsync(CancellationToken.None)).QuenchedDocuments;

        Assert.Equal(1, taken);
        Assert.Equal(["old-1", "old-2", "old-3"], world.Gateway.QuenchedUuids);
        Assert.Equal("UTILIZED", world.Incoming.Store["d-old"].Vsds.Single(item => item.Uuid == "old-3").Status);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d-mid"].Vsds.Single(item => item.Uuid == "mid").Status);
        Assert.Equal(VetisQuench.AutoAuthor, world.Incoming.Store["d-old"].QuenchedBy);
        Assert.Equal(VetisDateTime.Format(world.Clock.GetLocalNow()), world.Incoming.Store["d-old"].QuenchedAt);
        Assert.Equal("", world.Incoming.Store["d-mid"].QuenchedBy);
        Assert.Contains("Автогашение: погашено документов 1", world.Log.Lines);
    }

    [Fact]
    public async Task Организация_без_автогашения_и_выключенный_Ветис_не_читаются()
    {
        var world = new World();
        world.Organizations.Store["o2"] = Organization("o2", vetis: false, auto: true);
        world.Organizations.Store["o3"] = Organization("o3", vetis: true, auto: false);
        world.Incoming.Store["older"] = Document("older", "o3", "2026-09-01", Vsd("skip", "2026-09-01T08:00:00+07:00"));
        world.Incoming.Store["mine"] = Document("mine", "o1", "2026-10-01", Vsd("take", "2026-10-01T08:00:00+07:00"));
        world.Gateway.EnqueueQuench(Done("take"));

        await world.Sut.QuenchBatchAsync(CancellationToken.None);

        Assert.Equal(["take"], world.Gateway.QuenchedUuids);
        Assert.DoesNotContain("o2", world.Incoming.OldestOrganizations);
        Assert.DoesNotContain("o3", world.Incoming.OldestOrganizations);
    }

    [Fact]
    public async Task Выключенный_Ветис_сервера_ничего_не_шлёт()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.Enable = false;
        world.Incoming.Store["d1"] = Document("d1", "o1", "2026-10-01", Vsd("v1", "2026-10-01T08:00:00+07:00"));

        var batch = await world.Sut.QuenchBatchAsync(CancellationToken.None);

        Assert.Equal(0, batch.QuenchedDocuments);
        Assert.Equal("", batch.Error);
        Assert.Equal(0, world.Gateway.QuenchCalls);
        Assert.Equal(0, world.Incoming.OldestCalls);
    }

    [Fact]
    public async Task Задержка_и_запрет_автомобиля_оставляют_ВСД_оформленным()
    {
        var world = new World();
        world.Organizations.Store["o1"].VetisSettings.AutoQuenchDelayHours = 2;
        world.Parameters.Value.VetisSettings.RequireVehicleForQuench = true;
        world.Incoming.Store["d1"] = Document("d1", "o1", "2026-10-30",
            Vsd("fresh", "2026-10-30T11:00:00+07:00", "<vetDocument><vehicleNumber>А000АА77</vehicleNumber></vetDocument>"),
            Vsd("wagon", "2026-10-30T08:00:00+07:00", "<vetDocument><wagonNumber>123</wagonNumber></vetDocument>"),
            Vsd("car", "2026-10-30T09:00:00+07:00", "<vetDocument><vehicleNumber>А000АА77</vehicleNumber></vetDocument>"));
        world.Gateway.EnqueueQuench(Done("car"));

        await world.Sut.QuenchBatchAsync(CancellationToken.None);

        Assert.Equal(["car"], world.Gateway.QuenchedUuids);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "fresh").Status);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "wagon").Status);
    }

    [Fact]
    public async Task Документ_без_даты_идёт_после_датированного()
    {
        var world = new World();
        world.Incoming.Store["dated"] = Document("dated", "o1", "2026-10-20", Vsd("dated-vsd", "2026-10-20T08:00:00+07:00"));
        world.Incoming.Store["loose"] = Document("loose", "o1", "", Vsd("loose-vsd", "2026-10-01T08:00:00+07:00"));
        world.Gateway.EnqueueQuench(Done("dated-vsd"));
        world.Gateway.EnqueueQuench(Done("loose-vsd"));

        await world.Sut.QuenchBatchAsync(CancellationToken.None);

        Assert.Equal(["dated-vsd", "loose-vsd"], world.Gateway.QuenchedUuids);
    }

    [Fact]
    public async Task Обрыв_чтения_оставляет_число_уже_погашенных_документов()
    {
        var world = new World();
        world.Incoming.Store["d-old"] = Document("d-old", "o1", "2026-10-01", Vsd("old", "2026-10-01T08:00:00+07:00"));
        world.Incoming.Store["d-mid"] = Document("d-mid", "o1", "2026-10-15", Vsd("mid", "2026-10-15T08:00:00+07:00"));
        world.Incoming.FailFindIds.Add("d-mid");
        world.Gateway.EnqueueQuench(Done("old"));

        var batch = await world.Sut.QuenchBatchAsync(CancellationToken.None);

        Assert.Equal(1, batch.QuenchedDocuments);
        Assert.Equal("Журнал недоступен", batch.Error);
        Assert.Contains(world.Log.Lines, line => line.Contains("Автогашение: погашено документов 1"));
        Assert.Equal("UTILIZED", world.Incoming.Store["d-old"].Vsds.Single().Status);
    }

    [Fact]
    public async Task Несозревший_документ_не_съедает_запуск_следующий_круг_идёт_дальше()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.AutoQuenchBatchSize = 1;
        world.Incoming.Store["old"] = Document("old", "o1", "2026-09-01", Vsd("no-date", ""));
        world.Incoming.Store["next"] = Document("next", "o1", "2026-10-01", Vsd("due", "2026-10-01T08:00:00+07:00"));
        world.Gateway.EnqueueQuench(Done("due"));

        var first = await world.Sut.QuenchBatchAsync(null, CancellationToken.None);

        Assert.Equal(0, first.QuenchedDocuments);
        Assert.Empty(world.Gateway.QuenchedUuids);
        Assert.False(first.Finished);

        var second = await world.Sut.QuenchBatchAsync(first.Cursor, CancellationToken.None);

        Assert.Equal(1, second.QuenchedDocuments);
        Assert.Equal(["due"], world.Gateway.QuenchedUuids);
    }

    [Fact]
    public async Task После_полного_прохода_пропущенный_документ_читается_снова()
    {
        var world = new World();
        world.Parameters.Value.VetisSettings.AutoQuenchBatchSize = 1;
        world.Incoming.Store["old"] = Document("old", "o1", "2026-09-01", Vsd("no-date", ""));

        var first = await world.Sut.QuenchBatchAsync(null, CancellationToken.None);

        Assert.True(first.Finished);
        Assert.Equal(0, world.Gateway.QuenchCalls);
        var reads = world.Incoming.FindCalls;

        await world.Sut.QuenchBatchAsync(null, CancellationToken.None);

        Assert.True(world.Incoming.FindCalls > reads);
        Assert.Equal(0, world.Gateway.QuenchCalls);
    }

    [Fact]
    public async Task Отказ_Меркурия_попадает_в_ошибку_запуска()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document("d1", "o1", "2026-10-01", Vsd("v1", "2026-10-01T08:00:00+07:00"));
        world.Gateway.EnqueueQuench(new VetisExchange { Completed = false, Error = "отказ", Status = "REJECTED" });

        var batch = await world.Sut.QuenchBatchAsync(null, CancellationToken.None);

        Assert.Equal(0, batch.QuenchedDocuments);
        Assert.Equal("отказ", batch.Error);
        Assert.Contains(world.Log.Lines, line => line.Contains("d1") && line.Contains("отказ"));
    }

    [Fact]
    public async Task Занятый_документ_не_останавливает_пакет()
    {
        var world = new World();
        world.Incoming.Store["d-old"] = Document("d-old", "o1", "2026-10-01", Vsd("old", "2026-10-01T08:00:00+07:00"));
        world.Incoming.Store["d-mid"] = Document("d-mid", "o1", "2026-10-15", Vsd("mid", "2026-10-15T08:00:00+07:00"));
        world.Gateway.EnqueueQuench(Done("mid"));
        Assert.True(await world.Progress.TryEnter("d-old", CancellationToken.None));

        var taken = (await world.Sut.QuenchBatchAsync(CancellationToken.None)).QuenchedDocuments;

        Assert.Equal(1, taken);
        Assert.Equal(["mid"], world.Gateway.QuenchedUuids);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d-old"].Vsds.Single().Status);
    }

    private static VetisExchange Done(string uuid) => new()
    {
        Completed = true,
        Status = "COMPLETED",
        Operation = "ProcessIncomingConsignment",
        ResponseXml = $"""
            <root>
              <vetDocument>
                <uuid>{uuid}</uuid>
                <vetDStatus>UTILIZED</vetDStatus>
              </vetDocument>
            </root>
            """
    };

    private static VetisIncomingVsd Vsd(string uuid, string issueDate, string rawXml = "<vetDocument/>") => new()
    {
        Uuid = uuid,
        Status = "CONFIRMED",
        Volume = "10",
        IssueDate = issueDate,
        RawXml = rawXml
    };

    private static VetisIncomingDocumentEntity Document(string id, string organizationId, string date, params VetisIncomingVsd[] vsds) => new()
    {
        Id = id,
        OrganizationId = organizationId,
        EnterpriseGuid = "e1",
        Date = date,
        Status = VetisIncomingStatus.NotAccepted,
        SortAt = VetisIncomingDocumentEntity.DocumentSortAt(date),
        Vsds = vsds.ToList()
    };

    private static OrganizationEntity Organization(string id, bool vetis, bool auto) => new()
    {
        Id = id,
        Name = id,
        VetisSettings = new OrganizationVetisSettings
        {
            Enable = vetis,
            AutoQuench = auto,
            AutoQuenchDelayHours = 1,
            ApiKey = "key",
            BasicLogin = "user",
            BasicPassword = "secret",
            IssuerId = "issuer",
            InitiatorLogin = "operator"
        }
    };

    private sealed class World
    {
        public World()
        {
            Organizations.Store["o1"] = Organization("o1", vetis: true, auto: true);
            Clock = new FixedClock(Now);
            var recorder = new VetisApplicationRecorder(Applications, NullLogger<VetisApplicationRecorder>.Instance);
            var query = new VetisIncomingQueryService(Incoming, Organizations);
            var quench = new VetisIncomingQuenchService(Gateway, Incoming, Organizations, recorder, query, Parameters, Progress, Clock);
            Sut = new VetisIncomingAutoQuenchService(
                Incoming,
                Organizations,
                quench,
                Parameters,
                Log,
                Clock);
        }

        public FixedClock Clock { get; }

        public LineLog<VetisIncomingAutoQuenchService> Log { get; } = new();

        public ScriptedVetisGateway Gateway { get; } = new();

        public VetisQuenchProgressStore Progress { get; } = new();

        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryVetisIncoming Incoming { get; } = new();

        public MemoryVetisApplications Applications { get; } = new();

        public MemoryVetisParameters Parameters { get; } = new();

        public VetisIncomingAutoQuenchService Sut { get; }
    }
}

internal sealed class LineLog<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullLogger.Instance.BeginScope(state)!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Add(formatter(state, exception));
}
