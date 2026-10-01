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
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Истёкший сертификат нужен мониторингу: список для выбора его уже не показывает.
    /// </summary>
    [Fact]
    public async Task Preview_организация_отдаёт_срок_истёкшего_сертификата()
    {
        const string script = "return { message: organizations[0].name + \":\" + organizations[0].certificateWorkUntil };";
        var until = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Local);
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            DateTime.Now,
            organizations:
            [
                new OrganizationEntity
                {
                    Id = "o1",
                    Name = "Ромашка",
                    Inn = "246412218294",
                    TrueApiIntegrationSettings = new TrueApiIntegrationSettings { DigitalSignature = "ABC" }
                }
            ],
            certificates:
            [
                new DigitalSignature { Number = "ABC", WorkUntil = until }
            ]);

        var result = await sut.Preview(script);

        Assert.True(result.IsSuccess);
        Assert.Contains("Ромашка", result.Value.Message);
        Assert.Contains("2026-01-10", result.Value.Message);
    }

    [Fact]
    public async Task RunDueTemplates_организация_уходит_в_канал_своей_группы()
    {
        const string script = "return { items: organizations.map(function (o) { return o.name; }) };";
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            now,
            script,
            organizations:
            [
                new OrganizationEntity { Id = "o1", Name = "Ромашка" },
                new OrganizationEntity { Id = "o2", Name = "Василек" }
            ],
            group1OrganizationIds: ["o1"]);

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
        Assert.Contains(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("Ромашка"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("Василек"));
        Assert.Contains(sender.Sends, send => send.ChatId == 200 && send.Message.Contains("Василек"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 200 && send.Message.Contains("Ромашка"));
    }

    [Fact]
    public async Task Preview_скрипт_сертификатов_сообщает_об_истёкшем()
    {
        var script = AlertTemplateDefaults.All()
            .Single(template => template.Id == "check-organization-certificates")
            .Script;
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            DateTime.Now,
            organizations:
            [
                new OrganizationEntity
                {
                    Id = "o1",
                    Name = "Ромашка",
                    Inn = "246412218294",
                    TrueApiIntegrationSettings = new TrueApiIntegrationSettings { DigitalSignature = "ABC" }
                }
            ],
            certificates:
            [
                new DigitalSignature { Number = "ABC", WorkUntil = DateTime.Today.AddDays(-1) }
            ]);

        var result = await sut.Preview(script);

        Assert.True(result.IsSuccess);
        Assert.Contains("истёк", result.Value.Message);
        Assert.Contains("Ромашка", result.Value.Message);
    }

    [Fact]
    public async Task Preview_скрипт_лицензии_криптопро_сообщает_об_истёкшей()
    {
        var script = AlertTemplateDefaults.All()
            .Single(template => template.Id == "check-cryptopro-license")
            .Script;
        var yesterday = DateTime.Today.AddDays(-1);
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            DateTime.Now,
            licenseView: $"Expires: {yesterday:dd.MM.yyyy}");

        var result = await sut.Preview(script);

        Assert.True(result.IsSuccess);
        Assert.Contains("истекла", result.Value.Message);
    }

    [Fact]
    public async Task Preview_скрипт_лицензии_криптопро_понимает_expires_day()
    {
        var script = AlertTemplateDefaults.All()
            .Single(template => template.Id == "check-cryptopro-license")
            .Script;
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            DateTime.Now,
            licenseView: "Expires: 1 day(s)");

        var result = await sut.Preview(script);

        Assert.True(result.IsSuccess);
        Assert.Contains("истекает", result.Value.Message);
    }

    /// <summary>
    /// Лицензия одна на машину: при включённом общем канале группы её не получают.
    /// </summary>
    [Fact]
    public async Task RunDueTemplates_лицензия_криптопро_уходит_только_в_общий_канал()
    {
        const string script = "return { message: (cryptoProLicense && cryptoProLicense.expiresAt) ? cryptoProLicense.expiresAt : \"none\" };";
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(
            sender,
            globalEnabled: true,
            now,
            script,
            licenseView: "Expires: 18.11.2026");

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
        Assert.Contains(sender.Sends, send => send.ChatId == 200 && send.Message.Contains("2026-11-18"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("2026-11-18"));
    }

    [Fact]
    public async Task RunDueTemplates_лицензия_криптопро_идёт_в_группу_если_общий_канал_выключен()
    {
        const string script = "return { message: (cryptoProLicense && cryptoProLicense.expiresAt) ? cryptoProLicense.expiresAt : \"none\" };";
        var sender = new FakeMessageService("tg");
        var now = DateTime.Now;
        var sut = CreateSut(
            sender,
            globalEnabled: false,
            now,
            script,
            licenseView: "Expires: 18.11.2026");

        var result = await sut.RunDueTemplates(now);

        Assert.True(result.IsSuccess);
        Assert.Contains(sender.Sends, send => send.ChatId == 100 && send.Message.Contains("2026-11-18"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 200);
    }

    [Fact]
    public async Task Preview_логирует_если_лицензию_криптопро_не_прочитать()
    {
        var logger = new ListLogger<AlertTemplateRunService>();
        var sut = CreateSut(new FakeMessageService("tg"), globalEnabled: true, DateTime.Now, logger: logger);

        var result = await sut.Preview("return { message: \"ok\" };");

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Messages, message => message.Contains("Лицензию КриптоПро прочитать не удалось"));
    }

    [Fact]
    public async Task Preview_логирует_организацию_чей_сертификат_не_в_хранилище()
    {
        var logger = new ListLogger<AlertTemplateRunService>();
        var sut = CreateSut(
            new FakeMessageService("tg"),
            globalEnabled: true,
            DateTime.Now,
            logger: logger,
            licenseView: "Expires: 18.11.2026",
            organizations:
            [
                new OrganizationEntity
                {
                    Id = "o1",
                    Name = "Ромашка",
                    Inn = "246412218294",
                    TrueApiIntegrationSettings = new TrueApiIntegrationSettings { DigitalSignature = "ABC" }
                }
            ]);

        var result = await sut.Preview("return { message: \"ok\" };");

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Messages, message => message.Contains("Ромашка") && message.Contains("ABC"));
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
        IReadOnlyList<OrganizationEntity>? organizations = null,
        IReadOnlyList<DigitalSignature>? certificates = null,
        IReadOnlyList<string>? group1OrganizationIds = null,
        string? licenseView = null,
        ILogger<AlertTemplateRunService>? logger = null)
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
            OrganizationIds = group1OrganizationIds?.ToList() ?? [],
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
            logger ?? NullLogger<AlertTemplateRunService>.Instance,
            templates,
            new FakeTemplateManager(),
            new AlertDatasetScriptExecutor(),
            instances,
            new FakeStatisticsRepository(statistics),
            new FakeViolationsRepository(violations),
            new FakeOrganizationRepository(organizations),
            parameters,
            groups,
            factory,
            new FakeDigitalSignatureService(certificates),
            new FakeCryptoProLicenseService(licenseView));
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

        public Task<Result> ClearOrganizationLink(string organizationId)
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

    private sealed class FakeDigitalSignatureService : IDigitalSignatureService
    {
        private readonly List<DigitalSignature> _certificates;

        public FakeDigitalSignatureService(IReadOnlyList<DigitalSignature>? certificates = null)
        {
            _certificates = certificates?.ToList() ?? [];
        }

        public List<DigitalSignature> List() =>
            _certificates.Where(certificate => certificate.WorkUntil > DateTime.Now).ToList();

        public List<DigitalSignature> ListIncludingExpired() => _certificates;

        public Result Install(Stream archive) => throw new NotImplementedException();
    }

    private sealed class FakeCryptoProLicenseService : ICryptoProLicenseService
    {
        private readonly string? _viewText;

        public FakeCryptoProLicenseService(string? viewText)
        {
            _viewText = viewText;
        }

        public Result<string> View() => _viewText == null
            ? Result.Failure<string>("нет")
            : Result.Success(_viewText);

        public Result<string> Set(string serial) => throw new NotImplementedException();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
