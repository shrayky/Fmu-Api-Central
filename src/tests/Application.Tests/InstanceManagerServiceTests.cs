using Application.Instance.Services;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.SettingsSchema;
using Domain.Entitys.SettingsSchema.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class InstanceManagerServiceTests
{
    /// <summary>
    /// Удаление узла снимает и его статистику проверок, в том числе документы без nodeId.
    /// </summary>
    [Fact]
    public async Task Delete_удаляет_статистику_инстанса()
    {
        var instances = new FakeInstanceRepository();
        instances.Store["shop-1"] = new InstanceEntity { Id = "shop-1" };
        instances.Store["shop-2"] = new InstanceEntity { Id = "shop-2" };

        var date = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
        var stats = new FakeStatisticsRepository();
        stats.Records.Add(new MarkCheckStatisticsEntity
        {
            Id = $"shop-1_{date}",
            NodeId = "shop-1",
            Date = date
        });
        stats.Records.Add(new MarkCheckStatisticsEntity
        {
            Id = $"shop-1_{date + 86400}",
            NodeId = "",
            Date = date + 86400
        });
        stats.Records.Add(new MarkCheckStatisticsEntity
        {
            Id = $"shop-2_{date}",
            NodeId = "shop-2",
            Date = date
        });

        var sut = CreateSut(instances, stats);

        var deleted = await sut.Delete("shop-1");

        Assert.True(deleted);
        Assert.False(instances.Store.ContainsKey("shop-1"));
        Assert.DoesNotContain(stats.Records, record => record.ResolvedNodeId() == "shop-1");
        Assert.Contains(stats.Records, record => record.ResolvedNodeId() == "shop-2");
    }

    private static InstanceManagerService CreateSut(
        FakeInstanceRepository instances,
        FakeStatisticsRepository stats)
        => new(
            NullLogger<IInstanceManagerService>.Instance,
            instances,
            new UnusedGroupRepository(),
            new UnusedSettingsSchemaRepository(),
            new ServiceCollection().BuildServiceProvider(),
            stats,
            new UnusedParametersService());

    private sealed class FakeInstanceRepository : IInstanceRepository
    {
        public Dictionary<string, InstanceEntity> Store { get; } = new();

        public Task<Result> Update(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<InstanceEntity>> ByToken(string token)
            => Task.FromResult(Store.TryGetValue(token, out var entity)
                ? Result.Success(entity)
                : Result.Failure<InstanceEntity>($"Не найден fmu-api с токеном {token}"));

        public Task<Result<PaginatedResponse<InstanceEntity>>> List(
            int pageNumber, int pageSize, InstanceListFilter filter)
            => throw new NotImplementedException();

        public Task<Result<bool>> CreateInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<bool>> DeleteInstance(InstanceEntity instance)
        {
            Store.Remove(instance.Id);
            return Task.FromResult(Result.Success(true));
        }

        public Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate)
            => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> All() => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> ByGroupIds(IReadOnlyList<string> groupIds)
            => throw new NotImplementedException();

        public Task<Result> ClearGroupLink(string groupId) => throw new NotImplementedException();
    }

    private sealed class FakeStatisticsRepository : IMarksCheckStatisticRepository
    {
        public List<MarkCheckStatisticsEntity> Records { get; } = [];

        public Task<Result> CreateNew(MarkCheckStatisticsEntity statisticsEntity)
            => throw new NotImplementedException();

        public Task<Result> AddRange(List<MarkCheckStatisticsEntity> markCheckStatisticsEntities)
            => throw new NotImplementedException();

        public Task<Result> Delete(string entityId) => throw new NotImplementedException();

        public Task<Result> DeleteByNodeId(string nodeId)
        {
            Records.RemoveAll(record => record.ResolvedNodeId() == nodeId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<List<MarkCheckStatisticsEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => throw new NotImplementedException();
    }

    private sealed class UnusedGroupRepository : IInstanceGroupRepository
    {
        public Task<Result> Create(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result<InstanceGroupEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> All() => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> ByListId(List<string> ids) => throw new NotImplementedException();

        public Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
            => throw new NotImplementedException();
    }

    private sealed class UnusedSettingsSchemaRepository : ISettingsSchemaRepository
    {
        public Task<Result> Create(SettingsSchemaEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(SettingsSchemaEntity entity) => throw new NotImplementedException();

        public Task<Result<SettingsSchemaEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<SettingsSchemaEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<SettingsSchemaEntity>> All() => throw new NotImplementedException();

        public Task<List<SettingsSchemaEntity>> ByListId(List<string> ids) => throw new NotImplementedException();
    }

    private sealed class UnusedParametersService : IParametersService
    {
        public Task<Parameters> Current() => throw new NotImplementedException();

        public Task<bool> Update(Parameters parameters) => throw new NotImplementedException();
    }
}
