using Application.MarkCheckStatistics.Services;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Dto;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;

namespace Application.Tests;

public class MarkCheckStatisticsServiceTests
{
    /// <summary>
    /// Документы без nodeId (запись через STJ) восстанавливают узел из id.
    /// </summary>
    [Fact]
    public async Task List_имя_инстанса_если_nodeId_пуст()
    {
        var date = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
        var stats = new FakeStatisticsRepository();
        stats.Records.Add(new MarkCheckStatisticsEntity
        {
            Id = $"shop-1_{date}",
            NodeId = "",
            Date = date,
            Total = 10,
            SuccessfulOnlineChecks = 8,
            SuccessfulOfflineChecks = 1
        });

        var instances = new FakeInstanceRepository();
        instances.Store["shop-1"] = new InstanceEntity { Id = "shop-1", Name = "Магазин 1" };

        var sut = new MarkCheckStatisticsService(stats, instances);

        var result = await sut.List(1, 50, new MarkCheckStatisticsListFilter());

        var row = Assert.Single(result.Content);
        Assert.Equal("Магазин 1", row.InstanceName);
    }

    /// <summary>
    /// Фильтр groupId оставляет только инстансы выбранной группы.
    /// </summary>
    [Fact]
    public async Task List_фильтрует_по_группе()
    {
        var date = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
        var stats = new FakeStatisticsRepository();
        stats.Records.Add(Statistic("shop-1", date));
        stats.Records.Add(Statistic("shop-2", date));

        var instances = new FakeInstanceRepository();
        instances.Store["shop-1"] = new InstanceEntity { Id = "shop-1", Name = "А", GroupId = "g1" };
        instances.Store["shop-2"] = new InstanceEntity { Id = "shop-2", Name = "Б", GroupId = "g2" };

        var sut = new MarkCheckStatisticsService(stats, instances);

        var result = await sut.List(1, 50, new MarkCheckStatisticsListFilter { GroupId = "g1" });

        var row = Assert.Single(result.Content);
        Assert.Equal("shop-1", row.Id);
    }

    private static MarkCheckStatisticsEntity Statistic(string nodeId, long date) => new()
    {
        Id = $"{nodeId}_{date}",
        NodeId = nodeId,
        Date = date,
        Total = 4,
        SuccessfulOnlineChecks = 3,
        SuccessfulOfflineChecks = 1
    };

    private sealed class FakeStatisticsRepository : IMarksCheckStatisticRepository
    {
        public List<MarkCheckStatisticsEntity> Records { get; } = [];

        public Task<Result> CreateNew(MarkCheckStatisticsEntity statisticsEntity) => throw new NotImplementedException();

        public Task<Result> AddRange(List<MarkCheckStatisticsEntity> markCheckStatisticsEntities)
            => throw new NotImplementedException();

        public Task<Result> Delete(string entityId) => throw new NotImplementedException();

        public Task<Result<List<MarkCheckStatisticsEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => Task.FromResult(Result.Success(Records));
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

        public Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate) => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> All() => Task.FromResult(Result.Success(Store.Values.ToList()));

        public Task<Result<List<InstanceEntity>>> ByGroupIds(IReadOnlyList<string> groupIds) => throw new NotImplementedException();

        public Task<Result> ClearGroupLink(string groupId) => throw new NotImplementedException();
    }
}
