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
    public async Task Accept_на_этапе_перевозчика_не_зовёт_шлюз()
    {
        var world = new World();
        var document = Document();
        document.LastError = EtrnDocumentState.StageUnavailable("Получение груза");
        world.Put(document);

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Sign },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnDocumentState.StageUnavailable("Получение груза"), result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
        Assert.Equal(EtrnDocumentStatus.Waiting, world.Incoming.Store[document.Id].Status);
    }

    [Fact]
    public async Task Accept_на_чужой_ящик_не_зовёт_шлюз()
    {
        var world = new World();
        var document = Document();
        document.Status = EtrnDocumentStatus.Foreign;
        document.LastError = EtrnDocumentState.ForeignNote("2PS-potok");
        world.Put(document);

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Sign },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnDocumentState.ForeignNote("2PS-potok"), result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
        Assert.Equal(EtrnDocumentStatus.Foreign, world.Incoming.Store[document.Id].Status);
    }

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
    public async Task Accept_завершённый_в_СБИС_закрывает_карточку()
    {
        var world = new World();
        world.Put(Document());
        world.Operator.EnqueueExecute(new EtrnExecuteResult
        {
            Completed = false,
            CloseDocument = true,
            Error = "Выполнение завершено успешно",
            Exchanges = [new EtrnExchange { Method = EtrnMethods.ReadDocument, Completed = true }]
        });

        var result = await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Выполнение завершено успешно", result.Error);
        var stored = world.Incoming.Store[Document().Id];
        Assert.Equal(EtrnDocumentStatus.Closed, stored.Status);
        Assert.Equal("Выполнение завершено успешно", stored.LastError);
        Assert.Null(stored.SendingAt);
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

    [Fact]
    public async Task Accept_пишет_титул_платформы_в_журнал()
    {
        var world = new World();
        world.Put(Document());
        const string body = """{"message":"Некорректный титул"}""";
        world.Operator.EnqueueExecute(new EtrnExecuteResult
        {
            Completed = false,
            Error = "Платформа ЭТрН ответила 400",
            Exchanges =
            [
                new EtrnExchange
                {
                    Method = EtrnMethods.OfdConsigneeTitle,
                    Completed = false,
                    Error = "400",
                    Parameters = "308924",
                    RequestBody = "<Файл/>",
                    ResponseBody = body
                }
            ]
        });

        var result = await world.Acceptance.AcceptAsync(
            Document().Id,
            new EtrnAcceptCommand { Intent = EtrnAcceptanceIntent.Reject, Comment = "брак" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        var row = Assert.Single(world.Requests.Store.Values);
        Assert.Equal(EtrnMethods.OfdConsigneeTitle, row.Method);
        Assert.Equal(body, row.ResponseBody);
        Assert.Equal("400", row.Error);
        Assert.False(row.Completed);
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
    public async Task Исправление_Платформы_отправляет_записанный_приём()
    {
        var world = new World();
        world.AsPlatform();
        var document = Document();
        document.Operator.Type = EtrnOperatorType.Ofd;
        document.Status = EtrnDocumentStatus.Accepted;
        document.Action = EtrnAcceptanceAction.Received;
        document.Lines[0].FactPlaces = 10;
        document.Lines[0].FactWeight = "100";
        world.Put(document);
        world.Operator.EnqueueExecute(new EtrnExecuteResult { Completed = true });

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand
            {
                Correction = true,
                CorrectionNumber = 1,
                CorrectionDate = "20.11.2026",
                Intent = EtrnAcceptanceIntent.Reject,
                Comment = "не брать из формы"
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal(1, world.Operator.ExecuteCalls);
        Assert.Equal(EtrnAcceptanceAction.Received, world.Operator.LastCommand!.Decision.Action);
        Assert.Equal(1, world.Operator.LastCommand.Correction!.Number);
        Assert.Equal("20.11.2026", world.Operator.LastCommand.Correction.Date);
        var stored = world.Incoming.Store[document.Id];
        Assert.Equal(EtrnDocumentStatus.Accepted, stored.Status);
        Assert.Equal("", stored.LastError);
        Assert.Equal(1, stored.CorrectionNumber);
        Assert.Equal("20.11.2026", stored.CorrectionDate);
        Assert.Equal(1, result.Value.CorrectionNumber);
        Assert.Equal("20.11.2026", result.Value.CorrectionDate);
    }

    [Fact]
    public async Task Исправление_без_номера_не_зовёт_шлюз()
    {
        var world = new World();
        world.AsPlatform();
        var document = Document();
        document.Operator.Type = EtrnOperatorType.Ofd;
        document.Status = EtrnDocumentStatus.Accepted;
        document.Action = EtrnAcceptanceAction.Received;
        world.Put(document);

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Correction = true, CorrectionNumber = 0, CorrectionDate = "20.11.2026" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnCorrection.BadNumber, result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[document.Id].Status);
    }

    [Fact]
    public async Task Исправление_СБИС_остаётся_закрытым()
    {
        var world = new World();
        var document = Document();
        document.Status = EtrnDocumentStatus.Accepted;
        world.Put(document);

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Correction = true, CorrectionNumber = 1, CorrectionDate = "20.11.2026" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EtrnDocumentState.AlreadyClosed, result.Error);
        Assert.Equal(0, world.Operator.ExecuteCalls);
    }

    [Fact]
    public async Task Сбой_исправления_не_возвращает_ожидание()
    {
        var world = new World();
        world.AsPlatform();
        var document = Document();
        document.Operator.Type = EtrnOperatorType.Ofd;
        document.Status = EtrnDocumentStatus.Rejected;
        document.Action = EtrnAcceptanceAction.Rejected;
        document.GeneralComment = "брак";
        world.Put(document);
        world.Operator.EnqueueExecute(new EtrnExecuteResult
        {
            Completed = false,
            Error = "Платформа ЭТрН ответила 400"
        });

        var result = await world.Acceptance.AcceptAsync(
            document.Id,
            new EtrnAcceptCommand { Correction = true, CorrectionNumber = 1, CorrectionDate = "20.11.2026" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Платформа ЭТрН ответила 400", result.Error);
        var stored = world.Incoming.Store[document.Id];
        Assert.Equal(EtrnDocumentStatus.Rejected, stored.Status);
        Assert.Equal("Платформа ЭТрН ответила 400", stored.LastError);
        Assert.Equal(1, stored.CorrectionNumber);
        Assert.Equal("20.11.2026", stored.CorrectionDate);
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

        public void AsPlatform()
        {
            Organizations.Store["o1"].EtrnConnections![0].Type = EtrnOperatorType.Ofd;
            Operator.Type = EtrnOperatorType.Ofd;
        }
    }
}
