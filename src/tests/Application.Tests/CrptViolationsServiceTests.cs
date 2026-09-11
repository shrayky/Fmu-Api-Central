using Application.CrptViolations.Services;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Dto;
using Domain.Entitys.CrptViolations.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;

namespace Application.Tests;

public class CrptViolationsServiceTests
{
    /// <summary>
    /// Дашборд разворачивает отклонения в строки и считает KPI по документам дня.
    /// </summary>
    [Fact]
    public async Task List_агрегирует_строки_и_штраф_по_документам()
    {
        var date = new DateTimeOffset(new DateTime(2026, 9, 1)).ToUnixTimeSeconds();
        var stats = new FakeViolationsRepository();
        stats.Records.Add(new CrptViolationsDailyEntity
        {
            Id = "246412218294_20260901",
            Inn = "246412218294",
            Date = date,
            PenaltyAmountRub = 1500,
            Violations =
            [
                new CrptViolationItem
                {
                    ProductGroup = 8,
                    Region = "Красноярский край",
                    ViolationNumber = 2,
                    ViolationResult = "50",
                    ViolationResultName = "Просрочка"
                },
                new CrptViolationItem
                {
                    ProductGroup = 8,
                    Region = "Красноярский край",
                    ViolationNumber = 3,
                    ViolationResult = "41",
                    ViolationResultName = "Нет нанесения"
                }
            ]
        });

        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(new OrganizationEntity
        {
            Inn = "246412218294",
            Name = "Ромашка"
        });

        var sut = new CrptViolationsService(stats, organizations);
        var result = await sut.List(1, 50, new CrptViolationsListFilter
        {
            DateFrom = new DateTime(2026, 9, 1),
            DateTo = new DateTime(2026, 9, 1)
        });

        Assert.Equal(5, result.TotalViolations);
        Assert.Equal(1500, result.TotalPenaltyAmountRub);
        Assert.Equal(2, result.Content.Count);
        Assert.Equal("Ромашка", result.Content[0].OrganizationName);
        Assert.Equal("Просрочка", result.Content[0].ViolationResultName);
    }

    /// <summary>
    /// Фильтр по ИНН оставляет только выбранную организацию.
    /// </summary>
    [Fact]
    public async Task List_фильтрует_по_инн()
    {
        var date = new DateTimeOffset(new DateTime(2026, 9, 1)).ToUnixTimeSeconds();
        var stats = new FakeViolationsRepository();
        stats.Records.Add(Day("111", date, 1));
        stats.Records.Add(Day("222", date, 4));

        var organizations = new FakeOrganizationRepository();
        organizations.Items.Add(new OrganizationEntity { Inn = "111", Name = "А" });
        organizations.Items.Add(new OrganizationEntity { Inn = "222", Name = "Б" });

        var sut = new CrptViolationsService(stats, organizations);
        var result = await sut.List(1, 50, new CrptViolationsListFilter
        {
            Inn = "222",
            DateFrom = new DateTime(2026, 9, 1),
            DateTo = new DateTime(2026, 9, 1)
        });

        var row = Assert.Single(result.Content);
        Assert.Equal("222", row.Inn);
        Assert.Equal(4, result.TotalViolations);
        Assert.Equal(20, result.TotalPenaltyAmountRub);
    }

    private static CrptViolationsDailyEntity Day(string inn, long date, int violationNumber) => new()
    {
        Id = $"{inn}_20260901",
        Inn = inn,
        Date = date,
        PenaltyAmountRub = 20,
        Violations =
        [
            new CrptViolationItem
            {
                ProductGroup = 8,
                ViolationNumber = violationNumber,
                ViolationResultName = "тест"
            }
        ]
    };

    private sealed class FakeViolationsRepository : ICrptViolationsRepository
    {
        public List<CrptViolationsDailyEntity> Records { get; } = [];

        public Task<Result<List<DateOnly>>> GetDates(string inn, DateOnly from, DateOnly to)
            => throw new NotImplementedException();
        public Task<Result<CrptViolationsDailyEntity>> Get(string id) => throw new NotImplementedException();
        public Task<Result> Upsert(CrptViolationsDailyEntity entity) => throw new NotImplementedException();
        public Task<Result<List<CrptViolationsDailyEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => Task.FromResult(Result.Success(Records));
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public List<OrganizationEntity> Items { get; } = [];

        public Task<Result> Create(OrganizationEntity entity) => throw new NotImplementedException();
        public Task<Result> Update(OrganizationEntity entity) => throw new NotImplementedException();
        public Task<Result<OrganizationEntity>> GetById(string id) => throw new NotImplementedException();
        public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();
        public Task<Result> Delete(string id) => throw new NotImplementedException();
        public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();
        public Task<List<OrganizationEntity>> All() => Task.FromResult(Items);
    }
}
