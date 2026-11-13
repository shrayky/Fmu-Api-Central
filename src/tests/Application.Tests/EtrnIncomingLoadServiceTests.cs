using Application.Etrn;
using Application.Tests.Fakes;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class EtrnIncomingLoadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Load_пишет_оператора_адрес_строки_и_статус()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection(name: "СБИС Север"));
        world.Operator.EnqueueFetch(ScriptedEtrnOperator.Fetching(ScriptedEtrnOperator.Document("d-1")));

        var saved = await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(1, saved);
        var document = Assert.Single(world.Incoming.Store.Values);
        Assert.Equal("doc:o1:c1:d-1", document.Id);
        Assert.Equal(EtrnDocumentStatus.Waiting, document.Status);
        Assert.Equal("ТН-1", document.Number);
        Assert.Equal("2026-11-04", document.Date);
        Assert.Equal("Москва, ул. Ленина, 1", document.DeliveryAddress);
        Assert.Equal("uid-1", document.Uid);
        Assert.Equal("c1", document.Operator.ConnectionId);
        Assert.Equal("СБИС Север", document.Operator.Name);
        Assert.Equal(10, Assert.Single(document.Lines).Places);
        Assert.Equal("e-last", world.Sync.Store["c1"].EventId);
        Assert.Equal("d-last", world.Sync.Store["c1"].DocumentId);
        Assert.Equal("11.10.2026 12.00.00", world.Sync.Store["c1"].EventAt);
        Assert.NotEqual("", world.Sync.Store["c1"].LastSuccessAt);
    }

    [Fact]
    public async Task Load_переименование_подключения_не_меняет_оператора_в_документе()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection(name: "Новое имя"));
        var stored = ScriptedEtrnOperator.Document("d-1").ToEntity(
            new EtrnOperatorContext { OrganizationId = "o1", Connection = Connection(name: "Старое имя") });
        stored.Operator.Name = "Старое имя";
        world.Incoming.Store[stored.Id] = MemoryEtrnIncoming.Clone(stored);
        world.Operator.EnqueueFetch(ScriptedEtrnOperator.Fetching(ScriptedEtrnOperator.Document("d-1")));

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal("Старое имя", world.Incoming.Store[stored.Id].Operator.Name);
    }

    [Fact]
    public async Task Load_сохранение_организации_не_стирает_пару_идентификаторов()
    {
        var world = new World();
        world.Sync.Store["c1"] = new EtrnSyncState
        {
            Id = EtrnSyncState.BuildId("c1"),
            OrganizationId = "o1",
            ConnectionId = "c1",
            EventId = "e1",
            DocumentId = "d1"
        };
        world.Organizations.Store["o1"] = Organization(
            connection: Connection(),
            eventId: "e1",
            documentId: "d1");

        var organization = world.Organizations.Store["o1"];
        organization.Name = "Переименована";
        await world.Organizations.Update(organization);

        Assert.Equal("e1", world.Sync.Store["c1"].EventId);
        Assert.Equal("d1", world.Sync.Store["c1"].DocumentId);
    }

    [Fact]
    public async Task Load_документ_без_количества_сохраняется_с_ошибкой()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var document = ScriptedEtrnOperator.Document("d-1");
        document.Lines = [new EtrnCargoLine { Index = 1, Name = "Без количества" }];
        world.Operator.EnqueueFetch(ScriptedEtrnOperator.Fetching(document));

        await world.Loader.LoadAsync(CancellationToken.None);

        var stored = Assert.Single(world.Incoming.Store.Values);
        Assert.Equal(EtrnDocumentStatus.Waiting, stored.Status);
        Assert.Equal(EtrnDocumentStatus.NoQuantityError, stored.LastError);
        Assert.False(stored.CanDecide);
    }

    [Fact]
    public async Task Load_ошибка_шлюза_пишет_текст_и_не_двигает_пару()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        world.Sync.Store["c1"] = new EtrnSyncState
        {
            Id = EtrnSyncState.BuildId("c1"),
            OrganizationId = "o1",
            ConnectionId = "c1",
            EventId = "e1",
            DocumentId = "d1",
            EventAt = "11.10.2026 12.00.00"
        };
        world.Operator.EnqueueFetch(new EtrnFetchResult
        {
            Completed = false,
            Error = "Нет связи с Saby",
            Position = new EtrnPollPosition("e1", "d1", "11.10.2026 12.00.00")
        });

        var saved = await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(0, saved);
        Assert.Equal("e1", world.Sync.Store["c1"].EventId);
        Assert.Equal("d1", world.Sync.Store["c1"].DocumentId);
        Assert.Equal("11.10.2026 12.00.00", world.Operator.LastContext!.Position.EventAt);
        Assert.Equal("Нет связи с Saby", world.Sync.Store["c1"].LastError);
        Assert.Empty(world.Incoming.Store);
    }

    [Fact]
    public async Task Load_без_сертификата_пишет_ошибку_и_не_зовёт_шлюз()
    {
        var world = new World();
        var organization = Organization(connection: Connection());
        organization.TrueApiIntegrationSettings.DigitalSignature = "";
        world.Organizations.Store["o1"] = organization;

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(0, world.Operator.FetchCalls);
        Assert.Equal("Не выбран сертификат организации", world.Sync.Store["c1"].LastError);
    }

    [Fact]
    public async Task Load_пропускает_выключенное_подключение()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection(enable: false));

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(0, world.Operator.FetchCalls);
        Assert.Empty(world.Sync.Store);
    }

    [Fact]
    public async Task Load_снимает_зависший_Sending_по_этапу()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var document = new EtrnIncomingDocumentEntity
        {
            Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-1"),
            OrganizationId = "o1",
            Operator = EtrnOperator.For(Connection()),
            Status = EtrnDocumentStatus.Sending,
            SendingAt = Now.AddMinutes(-11),
            SendingAtUnix = Now.AddMinutes(-11).ToUnixTimeSeconds(),
            Lines = [new EtrnCargoLine { Index = 1, Places = 1, GrossWeight = "1" }]
        };
        world.Incoming.Store[document.Id] = document;
        world.Operator.EnqueueStage(new EtrnStageResult
        {
            Completed = true,
            Stage = EtrnPollEvent.AcceptanceStage,
            StillAcceptance = true
        });

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Waiting, world.Incoming.Store[document.Id].Status);
    }

    [Fact]
    public async Task Load_закрывает_зависший_Sending_на_другом_этапе()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var document = new EtrnIncomingDocumentEntity
        {
            Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-1"),
            OrganizationId = "o1",
            Operator = EtrnOperator.For(Connection()),
            Status = EtrnDocumentStatus.Sending,
            SendingAt = Now.AddMinutes(-11),
            SendingAtUnix = Now.AddMinutes(-11).ToUnixTimeSeconds()
        };
        world.Incoming.Store[document.Id] = document;
        world.Operator.EnqueueStage(new EtrnStageResult { Completed = true, Stage = "Подписание" });

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Closed, world.Incoming.Store[document.Id].Status);
    }

    [Fact]
    public async Task Load_закрывает_документ_если_этап_приёмки_закрыт_не_нами()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var document = new EtrnIncomingDocumentEntity
        {
            Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-1"),
            OrganizationId = "o1",
            OperatorDocumentId = "d-1",
            Operator = EtrnOperator.For(Connection()),
            Status = EtrnDocumentStatus.Waiting
        };
        world.Incoming.Store[document.Id] = document;
        world.Operator.EnqueueFetch(new EtrnFetchResult
        {
            Completed = true,
            Position = new EtrnPollPosition("e1", "d-1"),
            ClosedDocumentIds = ["d-1"]
        });

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Closed, world.Incoming.Store[document.Id].Status);
    }

    [Fact]
    public async Task Load_не_читает_ожидающие_накладные()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var waiting = Stored("d-1", EtrnDocumentStatus.Waiting);
        world.Incoming.Store[waiting.Id] = waiting;

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(0, world.Operator.StageCalls);
        Assert.Equal(EtrnDocumentStatus.Waiting, world.Incoming.Store[waiting.Id].Status);
        Assert.Equal(1, Assert.Single(world.Incoming.Store[waiting.Id].Lines).Places);
    }

    [Fact]
    public async Task Load_закрывает_ожидающую_накладную_по_состоянию_из_списка()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var waiting = Stored("d-1", EtrnDocumentStatus.Waiting);
        var accepted = Stored("d-2", EtrnDocumentStatus.Accepted);
        var sending = Stored("d-3", EtrnDocumentStatus.Sending);
        world.Incoming.Store[waiting.Id] = waiting;
        world.Incoming.Store[accepted.Id] = accepted;
        world.Incoming.Store[sending.Id] = sending;
        world.Operator.EnqueueFetch(new EtrnFetchResult
        {
            Completed = true,
            Position = new EtrnPollPosition("e1", "d-1"),
            ListedStates =
            [
                new EtrnListedState
                {
                    OperatorDocumentId = "d-1",
                    Status = EtrnDocumentStatus.Closed,
                    Error = "Выполнение завершено успешно"
                },
                new EtrnListedState
                {
                    OperatorDocumentId = "d-3",
                    Status = EtrnDocumentStatus.Closed,
                    Error = "Выполнение завершено успешно"
                },
                new EtrnListedState
                {
                    OperatorDocumentId = "нет-в-базе",
                    Status = EtrnDocumentStatus.Closed,
                    Error = "Выполнение завершено успешно"
                }
            ]
        });

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Closed, world.Incoming.Store[waiting.Id].Status);
        Assert.Equal("Выполнение завершено успешно", world.Incoming.Store[waiting.Id].LastError);
        Assert.Equal(1, Assert.Single(world.Incoming.Store[waiting.Id].Lines).Places);
        Assert.Equal(EtrnDocumentStatus.Closed, world.Incoming.Store[sending.Id].Status);
        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[accepted.Id].Status);
        Assert.Equal(0, world.Operator.StageCalls);
        Assert.False(world.Incoming.Store.ContainsKey(EtrnIncomingDocumentEntity.BuildId("o1", "c1", "нет-в-базе")));
    }

    [Fact]
    public async Task Load_сохраняет_частичное_действие_накладной()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var fromTitle = Stored("d-1", EtrnDocumentStatus.Waiting);
        var fromList = Stored("d-2", EtrnDocumentStatus.Waiting);
        world.Incoming.Store[fromTitle.Id] = fromTitle;
        world.Incoming.Store[fromList.Id] = fromList;
        var fetched = ScriptedEtrnOperator.Document("d-1");
        fetched.Status = EtrnDocumentStatus.Accepted;
        fetched.Action = EtrnAcceptanceAction.Partially;
        world.Operator.EnqueueFetch(new EtrnFetchResult
        {
            Completed = true,
            Position = new EtrnPollPosition("e1", "d-1"),
            Documents = [fetched],
            ListedStates =
            [
                new EtrnListedState
                {
                    OperatorDocumentId = "d-2",
                    Status = EtrnDocumentStatus.Accepted,
                    Action = EtrnAcceptanceAction.Partially
                }
            ]
        });

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[fromTitle.Id].Status);
        Assert.Equal(EtrnAcceptanceAction.Partially, world.Incoming.Store[fromTitle.Id].Action);
        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[fromList.Id].Status);
        Assert.Equal(EtrnAcceptanceAction.Partially, world.Incoming.Store[fromList.Id].Action);
    }

    [Fact]
    public async Task Open_сохраняет_частичное_действие()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var waiting = Stored("d-1", EtrnDocumentStatus.Waiting);
        world.Incoming.Store[waiting.Id] = waiting;
        world.Operator.EnqueueStage(new EtrnStageResult
        {
            Completed = true,
            Status = EtrnDocumentStatus.Accepted,
            Action = EtrnAcceptanceAction.Partially
        });

        await world.Loader.SyncOpenAsync(waiting.Id, CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[waiting.Id].Status);
        Assert.Equal(EtrnAcceptanceAction.Partially, world.Incoming.Store[waiting.Id].Action);
    }

    [Fact]
    public async Task Load_чужой_этап_из_списка_оставляет_ожидание()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var waiting = Stored("d-1", EtrnDocumentStatus.Waiting);
        world.Incoming.Store[waiting.Id] = waiting;
        world.Operator.EnqueueFetch(new EtrnFetchResult
        {
            Completed = true,
            ListedStates =
            [
                new EtrnListedState
                {
                    OperatorDocumentId = "d-1",
                    Status = EtrnDocumentStatus.Waiting,
                    Error = EtrnDocumentState.StageUnavailable("Получение груза")
                }
            ]
        });

        await world.Loader.LoadAsync(CancellationToken.None);

        var stored = world.Incoming.Store[waiting.Id];
        Assert.Equal(EtrnDocumentStatus.Waiting, stored.Status);
        Assert.Equal(EtrnDocumentState.StageUnavailable("Получение груза"), stored.LastError);
        Assert.Equal(0, world.Operator.StageCalls);
    }

    [Fact]
    public async Task Load_не_переписывает_чужой_ящик_в_ожидание()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var document = Stored("d-1", EtrnDocumentStatus.Foreign);
        document.LastError = EtrnDocumentState.ForeignNote("2PS-potok");
        world.Incoming.Store[document.Id] = document;
        world.Operator.EnqueueFetch(ScriptedEtrnOperator.Fetching(ScriptedEtrnOperator.Document("d-1")));

        await world.Loader.LoadAsync(CancellationToken.None);

        var stored = world.Incoming.Store[document.Id];
        Assert.Equal(EtrnDocumentStatus.Foreign, stored.Status);
        Assert.Equal(EtrnDocumentState.ForeignNote("2PS-potok"), stored.LastError);
    }

    [Fact]
    public async Task Open_ожидающую_СБИС_закрывает_если_приёмка_уже_получена()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var waiting = Stored("d-1", EtrnDocumentStatus.Waiting);
        world.Incoming.Store[waiting.Id] = waiting;
        world.Operator.EnqueueStage(new EtrnStageResult
        {
            Completed = true,
            Status = EtrnDocumentStatus.Accepted
        });

        await world.Loader.SyncOpenAsync(waiting.Id, CancellationToken.None);

        var stored = world.Incoming.Store[waiting.Id];
        Assert.Equal(EtrnDocumentStatus.Accepted, stored.Status);
        Assert.Equal("", stored.LastError);
        Assert.Equal(1, Assert.Single(stored.Lines).Places);
        Assert.Equal(1, world.Operator.StageCalls);
    }

    [Fact]
    public async Task Open_не_читает_принятую_и_чужого_оператора()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var accepted = Stored("d-1", EtrnDocumentStatus.Accepted);
        var ofd = Stored("d-2", EtrnDocumentStatus.Waiting);
        ofd.Operator = EtrnOperator.For(new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Ofd, Name = "Платформа ЭТрН" });
        world.Incoming.Store[accepted.Id] = accepted;
        world.Incoming.Store[ofd.Id] = ofd;

        await world.Loader.SyncOpenAsync(accepted.Id, CancellationToken.None);
        await world.Loader.SyncOpenAsync(ofd.Id, CancellationToken.None);

        Assert.Equal(0, world.Operator.StageCalls);
        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[accepted.Id].Status);
        Assert.Equal(EtrnDocumentStatus.Waiting, world.Incoming.Store[ofd.Id].Status);
    }

    [Fact]
    public async Task Load_не_переписывает_чужое_решение()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization(connection: Connection());
        var document = new EtrnIncomingDocumentEntity
        {
            Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-1"),
            OrganizationId = "o1",
            OperatorDocumentId = "d-1",
            Operator = EtrnOperator.For(Connection()),
            Status = EtrnDocumentStatus.Accepted
        };
        world.Incoming.Store[document.Id] = document;
        world.Operator.EnqueueFetch(new EtrnFetchResult
        {
            Completed = true,
            Position = new EtrnPollPosition("e1", "d-1"),
            ClosedDocumentIds = ["d-1"]
        });

        await world.Loader.LoadAsync(CancellationToken.None);

        Assert.Equal(EtrnDocumentStatus.Accepted, world.Incoming.Store[document.Id].Status);
    }

    private static EtrnIncomingDocumentEntity Stored(string operatorDocumentId, string status) => new()
    {
        Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", operatorDocumentId),
        OrganizationId = "o1",
        OperatorDocumentId = operatorDocumentId,
        Operator = EtrnOperator.For(Connection()),
        Status = status,
        Lines = [new EtrnCargoLine { Index = 1, Places = 1, GrossWeight = "1" }]
    };

    private static EtrnConnection Connection(string name = "СБИС", bool enable = true) =>
        new() { Id = "c1", Type = EtrnOperatorType.Saby, Name = name, Enable = enable };

    private static OrganizationEntity Organization(
        EtrnConnection connection,
        string eventId = "",
        string documentId = "") => new()
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
            connection
        ],
        VetisSettings = new OrganizationVetisSettings
        {
            Enterprises = []
        }
    };

    private sealed class World
    {
        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryEtrnIncoming Incoming { get; } = new();

        public MemoryEtrnSyncStates Sync { get; } = new();

        public MemoryEtrnRequests Requests { get; } = new();

        public ScriptedEtrnOperator Operator { get; } = new();

        public MemoryEtrnParameters Parameters { get; } = new();

        public FixedClock Clock { get; } = new(Now);

        public EtrnIncomingLoadService Loader { get; }

        public World()
        {
            Parameters.Value.EtrnSettings.IncomingFetchIntervalMinutes = 5;
            var recorder = new EtrnRequestRecorder(Requests, NullLogger<EtrnRequestRecorder>.Instance, Clock);
            Loader = new EtrnIncomingLoadService(
                [Operator],
                Organizations,
                Incoming,
                Sync,
                recorder,
                NullLogger<EtrnIncomingLoadService>.Instance,
                Clock);
        }
    }
}
