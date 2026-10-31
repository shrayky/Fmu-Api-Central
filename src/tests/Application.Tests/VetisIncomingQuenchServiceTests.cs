using Application.Tests.Fakes;
using Application.Vetis;
using Domain.Entitys.Organization;
using Domain.Vetis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class VetisIncomingQuenchServiceTests
{
    [Fact]
    public async Task Quench_гасит_оформленные_по_очереди_и_пишет_факт()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(
            Vsd("v1", "CONFIRMED", "10"),
            Vsd("v2", "CONFIRMED", "10"),
            Vsd("v3", "UTILIZED", "10"));
        world.Gateway.EnqueueQuench(Done("v1"));
        world.Gateway.EnqueueQuench(Done("v2"));

        var report = (await world.Sut.QuenchAsync("d1",
        [
            new VetisQuenchFact { Uuid = "v1", ActualVolume = "10" },
            new VetisQuenchFact { Uuid = "v2", ActualVolume = "4" }
        ], CancellationToken.None)).Value;

        Assert.Equal(2, report.Quenched);
        Assert.Equal(1, report.Skipped);
        Assert.Empty(report.Errors);
        Assert.Equal(2, world.Gateway.QuenchCalls);
        Assert.Equal(2, world.Applications.Store.Count);
        var saved = world.Incoming.Store["d1"];
        Assert.Equal(VetisIncomingStatus.Accepted, saved.Status);
        Assert.Equal("10", saved.Vsds.Single(item => item.Uuid == "v1").ActualVolume);
        Assert.Equal("4", saved.Vsds.Single(item => item.Uuid == "v2").ActualVolume);
        Assert.Equal("2026-10-30T08:00:00+07:00", saved.Vsds.Single(item => item.Uuid == "v1").ReceivedAt);
        Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v3").Status);
        Assert.Equal("4", report.Document!.Vsds.Single(item => item.Uuid == "v2").ActualVolume);
    }

    [Fact]
    public async Task Quench_после_ошибки_продолжает_следующий_ВСД()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(new VetisExchange { Completed = false, Error = "отказ", Status = "REJECTED" });
        world.Gateway.EnqueueQuench(Done("v2"));

        var report = (await world.Sut.QuenchAsync("d1", [], CancellationToken.None)).Value;

        Assert.Equal(1, report.Quenched);
        Assert.Equal("v1", Assert.Single(report.Errors).Uuid);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal("UTILIZED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v2").Status);
        Assert.Equal(VetisIncomingStatus.Partial, world.Incoming.Store["d1"].Status);
    }

    [Fact]
    public async Task Quench_уже_погашенный_в_Меркурии_пишет_статус_без_повтора_ошибки()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(new VetisExchange
        {
            Completed = false,
            Status = "REJECTED",
            Error = "Ветеринарно-сопроводительный документ должен быть в состоянии \"оформлен\" (т.е. не погашен)."
        });
        world.Gateway.EnqueueQuench(Done("v2"));

        var report = (await world.Sut.QuenchAsync("d1",
        [
            new VetisQuenchFact { Uuid = "v1", ActualVolume = "4" }
        ], CancellationToken.None)).Value;

        var saved = world.Incoming.Store["d1"];
        Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal("", saved.Vsds.Single(item => item.Uuid == "v1").ActualVolume);
        Assert.Equal("", saved.Vsds.Single(item => item.Uuid == "v1").ReceivedAt);
        Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v2").Status);
        Assert.Equal(VetisIncomingStatus.Accepted, saved.Status);
        Assert.Equal(2, report.Quenched);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task Quench_уже_погашенный_узнаётся_по_ёлочкам_и_пробелам()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(new VetisExchange
        {
            Completed = false,
            Status = "REJECTED",
            Error = "Ветеринарно-сопроводительный документ должен быть в состоянии «оформлен»  (т.е.  не погашен)."
        });

        var report = (await world.Sut.QuenchAsync("d1", [], CancellationToken.None)).Value;

        Assert.Equal("UTILIZED", world.Incoming.Store["d1"].Vsds.Single().Status);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task Quench_чужой_текст_с_обрывком_не_считается_погашенным()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(new VetisExchange
        {
            Completed = false,
            Status = "REJECTED",
            Error = "партия не погашена из-за акта"
        });
        world.Gateway.EnqueueQuench(Done("v2"));

        var report = (await world.Sut.QuenchAsync("d1", [], CancellationToken.None)).Value;

        var saved = world.Incoming.Store["d1"];
        Assert.Equal("CONFIRMED", saved.Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal("v1", Assert.Single(report.Errors).Uuid);
        Assert.Equal(1, report.Quenched);
        Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v2").Status);
    }

    [Fact]
    public async Task Quench_сохраняет_погашенный_ВСД_до_обрыва_следующего()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(Done("v1"));
        world.Gateway.ThrowFromCall = 2;

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Sut.QuenchAsync("d1", [], CancellationToken.None));

        Assert.Equal("UTILIZED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v2").Status);
    }

    [Fact]
    public async Task Quench_показывает_текущий_ВСД_и_очищает_состояние_после_всех()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(
            Vsd("v1", "CONFIRMED", "10", "Н-1"),
            Vsd("v2", "UTILIZED", "10", "Н-0"),
            Vsd("v3", "CONFIRMED", "10", "Н-3"));
        world.Gateway.EnqueueQuench(Done("v1"));
        world.Gateway.EnqueueQuench(Done("v3"));
        var seen = new List<VetisQuenchProgress>();
        world.Gateway.OnQuench = () => seen.Add(world.Progress.Find("d1")!);

        await world.Sut.QuenchAsync("d1", [], CancellationToken.None);

        Assert.Equal(2, seen.Count);
        Assert.Equal(1, seen[0].Index);
        Assert.Equal(2, seen[0].Total);
        Assert.Equal("v1", seen[0].Uuid);
        Assert.Equal("Н-1", seen[0].IssueNumber);
        Assert.Equal("sending", seen[0].Stage);
        Assert.True(seen[0].Running);
        Assert.Equal(2, seen[1].Index);
        Assert.Equal("v3", seen[1].Uuid);
        Assert.Equal("Н-3", seen[1].IssueNumber);
        Assert.Null(world.Progress.Find("d1"));
    }

    [Fact]
    public async Task Quench_очищает_состояние_если_Меркурий_оборвался()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10", "Н-1"));
        world.Gateway.ThrowFromCall = 1;
        VetisQuenchProgress? seen = null;
        world.Gateway.OnQuench = () => seen = world.Progress.Find("d1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Sut.QuenchAsync("d1", [], CancellationToken.None));

        Assert.Equal("v1", seen!.Uuid);
        Assert.Null(world.Progress.Find("d1"));
    }

    [Fact]
    public async Task Quench_без_оформленных_не_вызывает_Ветис()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "UTILIZED", "10"));

        var result = await world.Sut.QuenchAsync("d1", [], CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Нет ВСД для гашения.", result.Error);
        Assert.Equal(0, world.Gateway.QuenchCalls);
    }

    [Fact]
    public async Task Quench_не_гасит_ВСД_с_пустым_фактом()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(Done("v2"));

        var report = (await world.Sut.QuenchAsync("d1",
        [
            new VetisQuenchFact { Uuid = "v1", ActualVolume = " " }
        ], CancellationToken.None)).Value;

        Assert.Equal(1, world.Gateway.QuenchCalls);
        Assert.Equal("v1", Assert.Single(report.Errors).Uuid);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal("UTILIZED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v2").Status);
    }

    [Fact]
    public async Task Quench_не_отправляет_факт_вне_объёма()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(Done("v2"));

        var report = (await world.Sut.QuenchAsync("d1",
        [
            new VetisQuenchFact { Uuid = "v1", ActualVolume = "11" }
        ], CancellationToken.None)).Value;

        Assert.Equal(1, world.Gateway.QuenchCalls);
        Assert.Equal("v1", Assert.Single(report.Errors).Uuid);
        Assert.Equal("UTILIZED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v2").Status);
    }

    [Fact]
    public async Task Quench_не_стирает_ВСД_дописанный_пока_гашение_шло()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(Done("v1"));
        world.Gateway.EnqueueQuench(Done("v2"));
        world.Gateway.OnQuench = () =>
        {
            if (world.Gateway.QuenchCalls != 1)
                return;

            world.Incoming.Store["d1"].Vsds.Add(Vsd("v-new", "CONFIRMED", "1"));
        };

        var report = (await world.Sut.QuenchAsync("d1", [], CancellationToken.None)).Value;

        var saved = world.Incoming.Store["d1"];
        Assert.Equal(3, saved.Vsds.Count);
        Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v2").Status);
        Assert.Equal("CONFIRMED", saved.Vsds.Single(item => item.Uuid == "v-new").Status);
        Assert.Equal(2, report.Quenched);
    }

    [Fact]
    public async Task Quench_сбой_чтения_документа_не_выдаётся_за_пропажу_ВСД()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
        world.Gateway.EnqueueQuench(Done("v1"));
        world.Gateway.EnqueueQuench(Done("v2"));
        world.Gateway.OnQuench = () =>
        {
            if (world.Gateway.QuenchCalls == 1)
                world.Incoming.FindError = "CouchDB недоступна";
        };

        var result = await world.Sut.QuenchAsync("d1", [], CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("CouchDB недоступна", result.Error);
        Assert.DoesNotContain("уже нет", result.Error);
        Assert.Equal("CONFIRMED", world.Incoming.Store["d1"].Vsds.Single(item => item.Uuid == "v1").Status);
    }

    [Fact]
    public async Task Quench_повтор_того_же_документа_не_отправляется()
    {
        var world = new World();
        world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"));
        world.Gateway.Hold = true;
        world.Gateway.EnqueueQuench(Done("v1"));

        var first = world.Sut.QuenchAsync("d1", [], CancellationToken.None);
        await world.Gateway.Entered.Task;
        var second = await world.Sut.QuenchAsync("d1", [], CancellationToken.None);
        world.Gateway.Release.TrySetResult();
        var report = (await first).Value;

        Assert.True(second.IsFailure);
        Assert.Equal("Гашение уже выполняется.", second.Error);
        Assert.Equal(1, world.Gateway.QuenchCalls);
        Assert.Equal(1, report.Quenched);
        Assert.Null(world.Progress.Find("d1"));
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
                <statusChange><status>UTILIZED</status><actualDateTime>2026-10-30T08:00:00+07:00</actualDateTime></statusChange>
              </vetDocument>
            </root>
            """
    };

    private static VetisIncomingVsd Vsd(string uuid, string status, string volume, string issueNumber = "") => new()
    {
        Uuid = uuid,
        Status = status,
        Volume = volume,
        IssueNumber = issueNumber,
        RawXml = "<vetDocument/>"
    };

    private static VetisIncomingDocumentEntity Document(params VetisIncomingVsd[] vsds) => new()
    {
        Id = "d1",
        OrganizationId = "o1",
        EnterpriseGuid = "e1",
        Vsds = vsds.ToList()
    };

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
                    ApiKey = "key",
                    BasicLogin = "user",
                    BasicPassword = "secret",
                    IssuerId = "issuer",
                    InitiatorLogin = "operator"
                }
            };
            var recorder = new VetisApplicationRecorder(Applications, NullLogger<VetisApplicationRecorder>.Instance);
            var query = new VetisIncomingQueryService(Incoming, Organizations);
            Sut = new VetisIncomingQuenchService(Gateway, Incoming, Organizations, recorder, query, Parameters, Progress);
        }

        public ScriptedVetisGateway Gateway { get; } = new();

        public VetisQuenchProgressStore Progress { get; } = new();

        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryVetisIncoming Incoming { get; } = new();

        public MemoryVetisApplications Applications { get; } = new();

        public MemoryVetisParameters Parameters { get; } = new();

        public VetisIncomingQuenchService Sut { get; }
    }
}
