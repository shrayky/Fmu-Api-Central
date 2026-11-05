using Application.Etrn;
using Application.Tests.Fakes;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class EtrnAcceptanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accept_ошибка_решения_не_зовёт_шлюз_и_не_меняет_статус()
    {
        var world = new World();
        world.Put(Document());

        var result = await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand
            {
                Intent = EtrnAcceptanceIntent.Sign,
                Lines = [new EtrnAcceptanceFact { Index = 1, FactPlaces = 9, FactWeight = "100" }]
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnAcceptance.DifferenceNeedsComment, result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
        Assert.Equal(EtrnDocumentStatus.Waiting, world.Incoming.Store[Document().Id].Status);
    }

    [Fact]
    public async Task Accept_успех_идёт_через_Sending_и_пишет_факты()
    {
        var world = new World();
        world.Put(Document());
        world.Operator.EnqueueExecute(new EtrnExecuteResult
        {
            Completed = true,
            Exchanges =
            [
                new EtrnExchange { Method = EtrnMethods.WriteEnclosure, Completed = true, Parameters = "ТН-1" },
                new EtrnExchange { Method = EtrnMethods.PrepareAction, Completed = true, Parameters = "ТН-1" },
                new EtrnExchange { Method = EtrnMethods.ExecuteAction, Completed = true, Parameters = "ТН-1" }
            ]
        });

        var result = await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand
            {
                Intent = EtrnAcceptanceIntent.Sign,
                Comment = "общий",
                Lines =
                [
                    new EtrnAcceptanceFact { Index = 1, FactPlaces = 9, FactWeight = "99.5", Comment = "недовоз" }
                ]
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal(EtrnDocumentStatus.Sending, world.Operator.StatusAtExecute);
        var stored = world.Incoming.Store[Document().Id];
        Assert.Equal(EtrnDocumentStatus.Accepted, stored.Status);
        Assert.Equal(EtrnAcceptanceAction.Partially, stored.Action);
        Assert.Equal(9, stored.Lines[0].FactPlaces);
        Assert.Equal("99.5", stored.Lines[0].FactWeight);
        Assert.Equal("недовоз", stored.Lines[0].Comment);
        Assert.NotEqual("", stored.SignedAt);
        Assert.Equal(3, world.Requests.Store.Count);
    }

    [Fact]
    public async Task Accept_во_время_отправки_отклоняется()
    {
        var world = new World();
        var document = Document();
        document.Status = EtrnDocumentStatus.Sending;
        document.SendingAt = Now;
        world.Put(document);

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Sign },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnDocumentState.AlreadyRunning, result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
    }

    [Fact]
    public async Task Accept_сбой_шлюза_возвращает_ожидание()
    {
        var world = new World();
        world.Put(Document());
        world.Operator.EnqueueExecute(new EtrnExecuteResult
        {
            Completed = false,
            Error = new string('я', 400),
            Exchanges = [new EtrnExchange { Method = EtrnMethods.WriteEnclosure, Completed = false, Error = "сбой" }]
        });

        var result = await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        var stored = world.Incoming.Store[Document().Id];
        Assert.Equal(EtrnDocumentStatus.Waiting, stored.Status);
        Assert.Equal(300, stored.LastError.Length);
    }

    [Fact]
    public async Task Accept_не_пишет_вход_в_журнал()
    {
        var world = new World();
        world.Put(Document());
        world.Operator.EnqueueExecute(new EtrnExecuteResult
        {
            Completed = true,
            Exchanges =
            [
                new EtrnExchange { Method = EtrnMethods.AuthByCertificate, Completed = true },
                new EtrnExchange { Method = EtrnMethods.WriteEnclosure, Completed = true },
                new EtrnExchange { Method = EtrnMethods.PrepareAction, Completed = true },
                new EtrnExchange { Method = EtrnMethods.ExecuteAction, Completed = true }
            ]
        });

        await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);

        Assert.DoesNotContain(world.Requests.Store.Values, item => item.Method == EtrnMethods.AuthByCertificate);
        Assert.Equal(3, world.Requests.Store.Count);
    }

    [Theory]
    [InlineData(EtrnDocumentStatus.Accepted)]
    [InlineData(EtrnDocumentStatus.Rejected)]
    [InlineData(EtrnDocumentStatus.Closed)]
    public async Task Accept_конечный_статус_отклоняется(string status)
    {
        var world = new World();
        var document = Document();
        document.Status = status;
        world.Put(document);

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnDocumentState.AlreadyClosed, result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
    }

    [Fact]
    public async Task Accept_две_параллельные_команды_не_проходят_вместе()
    {
        var world = new World();
        world.Put(Document());
        world.Operator.HoldExecute = true;

        var first = world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);
        await world.Operator.ExecuteEntered.Task;

        var second = await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);

        world.Operator.ExecuteRelease.TrySetResult();
        await first;

        Assert.True(second.IsFailure);
        Assert.Equal(EtrnDocumentState.AlreadyRunning, second.Error);
        Assert.Equal(1, world.Operator.ExecuteCalls);
    }

    private static EtrnIncomingDocumentEntity Document() => new()
    {
        Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-1"),
        OrganizationId = "o1",
        Operator = new EtrnOperator { ConnectionId = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС" },
        Number = "ТН-1",
        Date = "2026-11-04",
        Status = EtrnDocumentStatus.Waiting,
        Lines = [new EtrnCargoLine { Index = 1, Name = "Ящики", Places = 10, GrossWeight = "100" }]
    };

    private sealed class World
    {
        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryEtrnIncoming Incoming { get; } = new();

        public MemoryEtrnRequests Requests { get; } = new();

        public ScriptedEtrnOperator Operator { get; } = new();

        public MemoryEtrnParameters Parameters { get; } = new();

        public FixedClock Clock { get; } = new(Now);

        public EtrnAcceptanceService Acceptance { get; }

        public World()
        {
            Organizations.Store["o1"] = new OrganizationEntity
            {
                Id = "o1",
                Name = "Организация",
                Inn = "7700000000",
                TrueApiIntegrationSettings = new TrueApiIntegrationSettings
                {
                    DigitalSignature = "serial-1",
                    Password = "pwd"
                },
                EtrnConnections =
                [
                    new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС", Enable = true }
                ]
            };

            var recorder = new EtrnRequestRecorder(Requests, NullLogger<EtrnRequestRecorder>.Instance, Clock);
            var query = new EtrnIncomingQueryService(Incoming, Organizations, new MemoryEtrnSyncStates());
            Acceptance = new EtrnAcceptanceService(
                [Operator],
                Organizations,
                Incoming,
                recorder,
                query,
                new EtrnAcceptanceGate(),
                NullLogger<EtrnAcceptanceService>.Instance,
                Clock);
        }

        public void Put(EtrnIncomingDocumentEntity document) =>
            Incoming.Store[document.Id] = MemoryEtrnIncoming.Clone(document);
    }
}
