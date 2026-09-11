using Application.AlertTemplates;
using Application.Tests.Fakes;
using CSharpFunctionalExtensions;
using Domain.Bot;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.AlertTemplates.Dto;
using Domain.Entitys.AlertTemplates.Interfaces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class AlertTemplateRunServiceTests
{
    private const string NamesScript = "return { items: instances.map(function (n) { return n.name; }) };";

    [Fact]
    public async Task RunDueTemplates_группа_шлёт_в_свой_канал_остальные_в_глобальный()
    {
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(sender, globalEnabled: true, now);

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
        Assert.Contains(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("a"));
        Assert.Contains(sender.Sends, send => send.ChatId == 200 && (send.Message.Contains("b") || send.Message.Contains("c")));
        Assert.DoesNotContain(sender.Sends, send =>
            send.ChatId == 100 && (send.Message.Contains("b") || send.Message.Contains("c")));
    }

    [Fact]
    public async Task RunDueTemplates_глобальный_выключен_шлёт_только_канал_группы()
    {
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(sender, globalEnabled: false, now);

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
        Assert.Contains(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("a"));
        Assert.DoesNotContain(sender.Sends, send => send.Message.Contains("b") || send.Message.Contains("c"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 200);
    }

    [Fact]
    public async Task Preview_контекст_из_всех_инстансов()
    {
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(sender, globalEnabled: true, now);

        var result = await sut.Preview(NamesScript);

        Assert.True(result.IsSuccess);
        Assert.Contains("a", result.Value.Items);
        Assert.Contains("b", result.Value.Items);
        Assert.Contains("c", result.Value.Items);
    }

    [Fact]
    public async Task RunDueTemplates_статистика_только_инстансов_группы()
    {
        const string script = "return { items: statistics.map(function (s) { return s.nodeId; }) };";
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(sender, globalEnabled: true, now, script, [
            new MarkCheckStatisticsEntity { NodeId = "a", Date = 1, Total = 1 },
            new MarkCheckStatisticsEntity { NodeId = "b", Date = 1, Total = 1 }
        ]);

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
        Assert.Contains(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("a"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("b"));
    }

    [Fact]
    public async Task Preview_violations_вчера_считает_штраф_и_отклонения()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var date = new DateTimeOffset(yesterday.ToDateTime(TimeOnly.MinValue)).ToUnixTimeSeconds();
        const string script =
            """
            const today = new Date(now);
            const y = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
            const pad = function (n) { return n < 10 ? "0" + n : "" + n; };
            const target = y.getFullYear() + "-" + pad(y.getMonth() + 1) + "-" + pad(y.getDate());
            const days = violations.filter(function (d) { return d.dateYmd === target; });
            var total = 0;
            var penalty = 0;
            days.forEach(function (d) {
                penalty += d.penaltyAmountRub;
                d.violations.forEach(function (v) { total += v.violationNumber; });
            });
            return { message: days[0].organizationName + ":" + total + ":" + penalty };
            """;

        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            now,
            violations:
            [
                new CrptViolationsDailyEntity
                {
                    Inn = "246412218294",
                    Date = date,
                    PenaltyAmountRub = 1500,
                    Violations =
                    [
                        new CrptViolationItem
                        {
                            ProductGroup = 8,
                            ViolationNumber = 2,
                            ViolationResultName = "Просрочка"
                        }
                    ]
                }
            ],
            organizations: [new OrganizationEntity { Inn = "246412218294", Name = "Ромашка" }]);

        var result = await sut.Preview(script);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ромашка:2:1500", result.Value.Message);
    }

    [Fact]
    public async Task RunDueTemplates_null_канал_группы_не_падает()
    {
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(sender, globalEnabled: true, now, nullGroupChannel: true);

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
    }

    private static AlertTemplateRunService CreateSut(
        FakeMessageService sender,
        bool globalEnabled,
        DateTime now,
        string? script = null,
        IReadOnlyList<MarkCheckStatisticsEntity>? statistics = null,
        bool nullGroupChannel = false,
        IReadOnlyList<CrptViolationsDailyEntity>? violations = null,
        IReadOnlyList<OrganizationEntity>? organizations = null)
    {
        var factory = new FakeMessageServiceFactory
        {
            Services = { [BotProvidersEnum.telegram] = sender }
        };

        var templates = new FakeTemplateRepository();
        templates.Enabled.Add(new AlertTemplateEntity
        {
            Id = "t1",
            Name = "names",
            Enabled = true,
            Script = script ?? NamesScript,
            Scheduler = [new AlertTemplateScheduleSlot { Time = now.ToString("HH:mm:ss") }]
        });

        var instances = new FakeInstanceRepository();
        instances.Store["a"] = new InstanceEntity { Id = "a", Name = "a", GroupId = "g1" };
        instances.Store["b"] = new InstanceEntity { Id = "b", Name = "b", GroupId = "g2" };
        instances.Store["c"] = new InstanceEntity { Id = "c", Name = "c" };

        var groups = new FakeGroupRepository();
        groups.Store["g1"] = new InstanceGroupEntity
        {
            Id = "g1",
            Name = "g1",
            AlertChannel = nullGroupChannel
                ? null!
                : new AlertChannel
                {
                    IsEnabled = true,
                    Provider = BotProvidersEnum.telegram,
                    ChatId = 100,
                    BotToken = "g-tok"
                }
        };
        groups.Store["g2"] = new InstanceGroupEntity
        {
            Id = "g2",
            Name = "g2",
            AlertChannel = new AlertChannel { IsEnabled = false }
        };

        var parameters = new FakeParametersService
        {
            Value = new Parameters
            {
                BotSettings = new Domain.Configuration.Options.TelegramBotSetting
                {
                    IsEnabled = globalEnabled,
                    Provider = BotProvidersEnum.telegram,
                    ChatId = 200,
                    BotToken = "glb-tok"
                }
            }
        };

        return new AlertTemplateRunService(
            NullLogger<AlertTemplateRunService>.Instance,
            templates,
            new FakeTemplateManager(),
            new AlertDatasetScriptExecutor(),
            instances,
            new FakeStatisticsRepository(statistics),
            new FakeViolationsRepository(violations),
            new FakeOrganizationRepository(organizations),
            parameters,
            groups,
            factory);
    }

    private sealed class FakeTemplateRepository : IAlertTemplateRepository
    {
        public List<AlertTemplateEntity> Enabled { get; } = [];

        public Task<Result> Create(AlertTemplateEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(AlertTemplateEntity entity) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<Result<AlertTemplateEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<AlertTemplateEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<AlertTemplateEntity>> All() => throw new NotImplementedException();

        public Task<List<AlertTemplateEntity>> AllEnabled() => Task.FromResult(Enabled);
    }

    private sealed class FakeTemplateManager : IAlertTemplateManager
    {
        public Task<Result> Create(AlertTemplateView data) => throw new NotImplementedException();

        public Task<Result> Update(AlertTemplateView data) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<AlertTemplateView>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<Result> EnsureDefaults() => Task.FromResult(Result.Success());
    }

    private sealed class FakeInstanceRepository : IInstanceRepository
    {
        public Dictionary<string, InstanceEntity> Store { get; } = new();

        public Task<Result> Update(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<InstanceEntity>> ByToken(string token) => throw new NotImplementedException();

        public Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, InstanceListFilter filter)
            => throw new NotImplementedException();

        public Task<Result<bool>> CreateInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<bool>> DeleteInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate)
            => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> All()
            => Task.FromResult(Result.Success(Store.Values.ToList()));

        public Task<Result<List<InstanceEntity>>> ByGroupIds(IReadOnlyList<string> groupIds)
            => throw new NotImplementedException();

        public Task<Result> ClearGroupLink(string groupId) => throw new NotImplementedException();
    }

    private sealed class FakeGroupRepository : IInstanceGroupRepository
    {
        public Dictionary<string, InstanceGroupEntity> Store { get; } = new();

        public Task<Result> Create(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result<InstanceGroupEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> All() => Task.FromResult(Store.Values.ToList());

        public Task<List<InstanceGroupEntity>> ByListId(List<string> ids) => throw new NotImplementedException();

        public Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
            => throw new NotImplementedException();
    }

    private sealed class FakeStatisticsRepository : IMarksCheckStatisticRepository
    {
        private readonly List<MarkCheckStatisticsEntity> _items;

        public FakeStatisticsRepository(IReadOnlyList<MarkCheckStatisticsEntity>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public Task<Result> CreateNew(MarkCheckStatisticsEntity statisticsEntity)
            => throw new NotImplementedException();

        public Task<Result> AddRange(List<MarkCheckStatisticsEntity> markCheckStatisticsEntities)
            => throw new NotImplementedException();

        public Task<Result> Delete(string entityId) => throw new NotImplementedException();

        public Task<Result> DeleteByNodeId(string nodeId) => throw new NotImplementedException();

        public Task<Result<List<MarkCheckStatisticsEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => Task.FromResult(Result.Success(_items));
    }

    private sealed class FakeViolationsRepository : ICrptViolationsRepository
    {
        private readonly List<CrptViolationsDailyEntity> _items;

        public FakeViolationsRepository(IReadOnlyList<CrptViolationsDailyEntity>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public Task<Result<List<DateOnly>>> GetDates(string inn, DateOnly from, DateOnly to)
            => throw new NotImplementedException();

        public Task<Result<CrptViolationsDailyEntity>> Get(string id) => throw new NotImplementedException();

        public Task<Result> Upsert(CrptViolationsDailyEntity entity) => throw new NotImplementedException();

        public Task<Result<List<CrptViolationsDailyEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => Task.FromResult(Result.Success(_items));
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        private readonly List<OrganizationEntity> _items;

        public FakeOrganizationRepository(IReadOnlyList<OrganizationEntity>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public Task<Result> Create(OrganizationEntity entity) => throw new NotImplementedException();
        public Task<Result> Update(OrganizationEntity entity) => throw new NotImplementedException();
        public Task<Result<OrganizationEntity>> GetById(string id) => throw new NotImplementedException();
        public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();
        public Task<Result> Delete(string id) => throw new NotImplementedException();
        public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();
        public Task<List<OrganizationEntity>> All() => Task.FromResult(_items);
    }

    private sealed class FakeParametersService : IParametersService
    {
        public Parameters Value { get; init; } = new();

        public Task<Parameters> Current() => Task.FromResult(Value);

        public Task<bool> Update(Parameters parameters) => throw new NotImplementedException();
    }
}
