using Application.CrptViolations.Services;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.TrueApiIntegration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class CrptViolationsLoaderServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 11);

    /// <summary>
    /// Без Enable и без ЭЦП клиент ЧЗ не вызывается.
    /// </summary>
    [Fact]
    public async Task Load_пропускает_организацию_без_интеграции_или_эцп()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("1", enable: false, signature: "thumb"));
        organizations.Items.Add(Org("2", enable: true, signature: ""));

        var client = new FakeStatisticsClient();
        var sut = CreateSut(organizations, new FakeApplicationState("1", "2"), client, new FakeViolationsRepository());

        await sut.Load(Today, CancellationToken.None);

        Assert.Empty(client.Calls);
    }

    /// <summary>
    /// Нет токена — день не запрашиваем, токен подтянет существующий лоадер.
    /// </summary>
    [Fact]
    public async Task Load_пропускает_организацию_без_токена()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("246412218294", enable: true, signature: "thumb"));

        var client = new FakeStatisticsClient();
        var sut = CreateSut(organizations, new FakeApplicationState(), client, new FakeViolationsRepository());

        await sut.Load(Today, CancellationToken.None);

        Assert.Empty(client.Calls);
    }

    /// <summary>
    /// Первая загрузка месяца: один снимок, документ только за сегодня, дельта равна снимку.
    /// </summary>
    [Fact]
    public async Task Load_первая_загрузка_только_сегодня()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("246412218294", enable: true, signature: "thumb"));

        var client = new FakeStatisticsClient
        {
            ViolationsJson = ViolationsJson(2),
            PenaltyJson = PenaltyJson(1500)
        };
        var repository = new FakeViolationsRepository();
        var sut = CreateSut(organizations, new FakeApplicationState("246412218294"), client, repository);

        await sut.Load(Today, CancellationToken.None);

        Assert.Single(repository.Store);
        Assert.True(repository.Store.ContainsKey("246412218294_20260911"));
        Assert.Equal(2, client.Calls.Count);
        Assert.All(client.Calls, call => Assert.Equal(Today, call.Date));
        Assert.Contains(client.Calls, call => call.DataSet == "violations");
        Assert.Contains(client.Calls, call => call.DataSet == "penalty");

        var today = repository.Store["246412218294_20260911"];
        Assert.Equal(2, today.SnapshotViolations[0].ViolationNumber);
        Assert.Equal(2, today.Violations[0].ViolationNumber);
        Assert.Equal(1500, today.SnapshotPenaltyAmountRub);
        Assert.Equal(1500, today.PenaltyAmountRub);
    }

    /// <summary>
    /// Повторная загрузка: снова один снимок, в документ пишем прирост от предыдущего дня.
    /// </summary>
    [Fact]
    public async Task Load_повторно_дельта_от_предыдущего_дня()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("246412218294", enable: true, signature: "thumb"));

        var repository = new FakeViolationsRepository();
        repository.Store["246412218294_20260910"] = new CrptViolationsDailyEntity
        {
            Id = "246412218294_20260910",
            Inn = "246412218294",
            SnapshotViolations = [new CrptViolationItem { ProductGroup = 8, Region = "Красноярский край", ViolationNumber = 2, ViolationResult = "50" }],
            SnapshotPenaltyAmountRub = 1000
        };

        var client = new FakeStatisticsClient
        {
            ViolationsJson = ViolationsJson(5),
            PenaltyJson = PenaltyJson(1800)
        };
        var sut = CreateSut(organizations, new FakeApplicationState("246412218294"), client, repository);

        await sut.Load(Today, CancellationToken.None);

        Assert.Equal(2, client.Calls.Count);
        Assert.Equal(2, repository.Store.Count);

        var today = repository.Store["246412218294_20260911"];
        Assert.Equal(5, today.SnapshotViolations[0].ViolationNumber);
        Assert.Equal(3, today.Violations[0].ViolationNumber);
        Assert.Equal(1800, today.SnapshotPenaltyAmountRub);
        Assert.Equal(800, today.PenaltyAmountRub);
    }

    /// <summary>
    /// Ошибка клиента на сегодняшнем снимке не создаёт документ дня.
    /// </summary>
    [Fact]
    public async Task Load_ошибка_клиента_не_пишет_сегодня()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("246412218294", enable: true, signature: "thumb"));

        var repository = new FakeViolationsRepository();
        repository.Store["246412218294_20260901"] = new CrptViolationsDailyEntity
        {
            Id = "246412218294_20260901",
            Inn = "246412218294"
        };

        var client = new FakeStatisticsClient { FailOnDate = Today };
        var sut = CreateSut(organizations, new FakeApplicationState("246412218294"), client, repository);

        var result = await sut.Load(Today, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(repository.Store.ContainsKey("246412218294_20260911"));
    }

    /// <summary>
    /// Ручной запрос при ошибке клиента возвращает причину, а не тихий успех.
    /// </summary>
    [Fact]
    public async Task Load_по_инн_ошибка_клиента()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("246412218294", enable: true, signature: "thumb"));

        var client = new FakeStatisticsClient { FailOnDate = Today };
        var sut = CreateSut(organizations, new FakeApplicationState("246412218294"), client, new FakeViolationsRepository());

        var result = await sut.Load(Today, CancellationToken.None, "246412218294");

        Assert.True(result.IsFailure);
        Assert.Contains("ошибка дня", result.Error);
    }

    /// <summary>
    /// Ручной запрос грузит только указанный ИНН.
    /// </summary>
    [Fact]
    public async Task Load_по_инн_не_трогает_другие_организации()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("111", enable: true, signature: "thumb"));
        organizations.Items.Add(Org("222", enable: true, signature: "thumb"));

        var client = new FakeStatisticsClient();
        var sut = CreateSut(organizations, new FakeApplicationState("111", "222"), client, new FakeViolationsRepository());

        var result = await sut.Load(Today, CancellationToken.None, "222");

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(client.Calls);
        Assert.All(client.Calls, call => Assert.Equal("222", call.Inn));
        Assert.DoesNotContain(client.Calls, call => call.Inn == "111");
    }

    /// <summary>
    /// Ручной запрос без токена возвращает ошибку, а не тихий пропуск.
    /// </summary>
    [Fact]
    public async Task Load_по_инн_без_токена_ошибка()
    {
        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(Org("246412218294", enable: true, signature: "thumb"));

        var sut = CreateSut(organizations, new FakeApplicationState(), new FakeStatisticsClient(), new FakeViolationsRepository());

        var result = await sut.Load(Today, CancellationToken.None, "246412218294");

        Assert.True(result.IsFailure);
        Assert.Contains("Токен True API", result.Error);
    }

    private static CrptViolationsLoaderService CreateSut(
        FakeOrganizationRepository organizations,
        FakeApplicationState state,
        FakeStatisticsClient client,
        FakeViolationsRepository repository)
        => new(organizations, state, client, repository, NullLogger<CrptViolationsLoaderService>.Instance);

    private static OrganizationEntity Org(string inn, bool enable, string signature) => new()
    {
        Inn = inn,
        TrueApiIntegrationSettings = new TrueApiIntegrationSettings
        {
            Enable = enable,
            DigitalSignature = signature
        }
    };

    private static string ViolationsJson(int violationNumber) =>
        $$"""
        {
            "analytical_violations_total_by_inn": [
                {
                    "product_group": 8,
                    "violations": [
                        {
                            "region": "Красноярский край",
                            "violation_number": {{violationNumber}},
                            "violation_result": "50",
                            "violation_result_name": "тест"
                        }
                    ]
                }
            ]
        }
        """;

    private static string PenaltyJson(int amount) =>
        $$"""{"analytical_violations_auto_penalty":[{"amount_rub":{{amount}}}]}""";

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public List<OrganizationEntity> Items { get; } = [];

        public Task<Result> Create(OrganizationEntity entity) => throw new NotImplementedException();
        public Task<Result> Update(OrganizationEntity entity) => throw new NotImplementedException();
        public Task<Result<OrganizationEntity>> GetById(string id) => throw new NotImplementedException();
        public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();
        public Task<Result> Delete(string id) => throw new NotImplementedException();
        public Task<Domain.Dto.Responces.PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();
        public Task<List<OrganizationEntity>> All() => Task.FromResult(Items);
    }

    private sealed class FakeApplicationState : IApplicationState
    {
        private readonly HashSet<string> _innsWithToken;

        public FakeApplicationState(params string[] innsWithToken)
        {
            _innsWithToken = [.. innsWithToken];
        }

        public void DbStateUpdate(bool isOnline) { }
        public bool DbState() => true;
        public void UpdateNeedRestart(bool need) { }
        public bool NeedRestart() => false;
        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil) { }
        public TrueApiToken TrueApiToken(string inn)
            => _innsWithToken.Contains(inn)
                ? new TrueApiToken { Inn = inn, Token = "token" }
                : new TrueApiToken();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }

    private sealed class FakeStatisticsClient : ICrptStatisticsClient
    {
        public List<(string DataSet, DateOnly Date, string Inn)> Calls { get; } = [];
        public DateOnly? FailOnDate { get; set; }
        public string ViolationsJson { get; set; } = "{}";
        public string PenaltyJson { get; set; } = """{"analytical_violations_auto_penalty":[{"amount_rub":0}]}""";

        public Task<Result<string>> FetchViolations(string inn, string token, DateOnly date, CancellationToken cancellationToken)
        {
            Calls.Add(("violations", date, inn));
            if (FailOnDate == date)
                return Task.FromResult(Result.Failure<string>("ошибка дня"));

            return Task.FromResult(Result.Success(ViolationsJson));
        }

        public Task<Result<string>> FetchPenalty(string inn, string token, DateOnly date, CancellationToken cancellationToken)
        {
            Calls.Add(("penalty", date, inn));
            return Task.FromResult(Result.Success(PenaltyJson));
        }
    }

    private sealed class FakeViolationsRepository : ICrptViolationsRepository
    {
        public Dictionary<string, CrptViolationsDailyEntity> Store { get; } = [];

        public Task<Result<List<DateOnly>>> GetDates(string inn, DateOnly from, DateOnly to)
        {
            var dates = Store.Values
                .Where(item => item.Inn == inn)
                .Select(item =>
                {
                    var separator = item.Id.LastIndexOf('_');
                    return DateOnly.ParseExact(item.Id[(separator + 1)..], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                })
                .Where(date => date >= from && date <= to)
                .ToList();

            return Task.FromResult(Result.Success(dates));
        }

        public Task<Result<CrptViolationsDailyEntity>> Get(string id)
            => Store.TryGetValue(id, out var entity)
                ? Task.FromResult(Result.Success(entity))
                : Task.FromResult(Result.Failure<CrptViolationsDailyEntity>($"Не найден документ отклонений ЧЗ {id}"));

        public Task<Result> Upsert(CrptViolationsDailyEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<List<CrptViolationsDailyEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => throw new NotImplementedException();
    }
}
