using Application.Instance.Services;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Dto.FmuApiExchangeData.DataPacket;
using Domain.Dto.FmuApiExchangeData.Request;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Entitys.SettingsSchema;
using Domain.Entitys.SettingsSchema.Interfaces;
using Domain.TrueApiIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

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

    /// <summary>
    /// Сеанс обмена отдаёт токены только организаций группы инстанса.
    /// </summary>
    [Fact]
    public async Task Update_отдаёт_токен_TrueApi_по_группе()
    {
        var instances = new FakeInstanceRepository();
        instances.Store["shop-1"] = new InstanceEntity { Id = "shop-1", GroupId = "g1" };

        var organizations = new FakeOrganizationRepository();
        organizations.Store.Add(new OrganizationEntity { Id = "o1", Inn = "7701234567" });
        organizations.Store.Add(new OrganizationEntity { Id = "o2", Inn = "7707654321" });

        var groups = new FakeGroupRepository();
        groups.Store["g1"] = new InstanceGroupEntity
        {
            Id = "g1",
            OrganizationIds = ["o1"]
        };

        var expired = new DateTime(2026, 9, 12, 10, 0, 0);
        var state = new FakeApplicationState();
        state.Tokens.Add(new TrueApiToken { Inn = "7701234567", Token = "live-token", LiveUntil = expired });
        state.Tokens.Add(new TrueApiToken { Inn = "7707654321", Token = "other-token", LiveUntil = expired });

        var sut = CreateSut(instances, new FakeStatisticsRepository(), organizations, state, groups);
        var packet = JsonSerializer.Serialize(new DataPacket
        {
            Token = "shop-1",
            Data = JsonSerializer.Serialize(new Payload())
        });

        var result = await sut.UpdateFmuApiInstanceInformation(packet);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.TrueApiTokens);
        Assert.Equal("7701234567", item.Inn);
        Assert.Equal("live-token", item.Token);
        Assert.Equal(expired, item.Expired);
    }

    /// <summary>
    /// Новый инстанс не помечаем к выгрузке настроек.
    /// </summary>
    [Fact]
    public async Task CreateNew_не_ставит_выгрузку_настроек()
    {
        var instances = new FakeInstanceRepository();
        var sut = CreateSut(instances, new FakeStatisticsRepository());

        var created = await sut.CreateNew(new InstanceMonitoringInformation
        {
            Token = "shop-1",
            Name = "Магазин",
            SettingsModified = true
        });

        Assert.True(created);
        Assert.False(instances.Store["shop-1"].SettingsModified);
    }

    private static InstanceManagerService CreateSut(
        FakeInstanceRepository instances,
        FakeStatisticsRepository stats,
        FakeOrganizationRepository? organizations = null,
        FakeApplicationState? state = null,
        FakeGroupRepository? groups = null)
        => new(
            NullLogger<IInstanceManagerService>.Instance,
            instances,
            groups ?? new FakeGroupRepository(),
            new UnusedSettingsSchemaRepository(),
            new ServiceCollection().BuildServiceProvider(),
            stats,
            new StubParametersService(),
            organizations ?? new FakeOrganizationRepository(),
            state ?? new FakeApplicationState());

    private sealed class FakeInstanceRepository : IInstanceRepository
    {
        public Dictionary<string, InstanceEntity> Store { get; } = new();

        public Task<Result> Update(InstanceEntity instance) => Task.FromResult(Result.Success());

        public Task<Result<InstanceEntity>> ByToken(string token)
            => Task.FromResult(Store.TryGetValue(token, out var entity)
                ? Result.Success(entity)
                : Result.Failure<InstanceEntity>($"Не найден fmu-api с токеном {token}"));

        public Task<Result<PaginatedResponse<InstanceEntity>>> List(
            int pageNumber, int pageSize, InstanceListFilter filter)
            => throw new NotImplementedException();

        public Task<Result<bool>> CreateInstance(InstanceEntity instance)
        {
            Store[instance.Id] = instance;
            return Task.FromResult(Result.Success(true));
        }

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
            => Task.FromResult(Result.Success());

        public Task<Result> Delete(string entityId) => throw new NotImplementedException();

        public Task<Result> DeleteByNodeId(string nodeId)
        {
            Records.RemoveAll(record => record.ResolvedNodeId() == nodeId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<List<MarkCheckStatisticsEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
            => throw new NotImplementedException();
    }

    private sealed class FakeGroupRepository : IInstanceGroupRepository
    {
        public Dictionary<string, InstanceGroupEntity> Store { get; } = new();

        public Task<Result> Create(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result<InstanceGroupEntity>> GetById(string id)
            => Task.FromResult(Store.TryGetValue(id, out var entity)
                ? Result.Success(entity)
                : Result.Failure<InstanceGroupEntity>($"Группа {id} не найдена"));

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

    private sealed class StubParametersService : IParametersService
    {
        public Task<Parameters> Current() => Task.FromResult(new Parameters());

        public Task<bool> Update(Parameters parameters) => throw new NotImplementedException();
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public List<OrganizationEntity> Store { get; } = [];

        public Task<Result> Create(OrganizationEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(OrganizationEntity entity) => throw new NotImplementedException();

        public Task<Result<OrganizationEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<OrganizationEntity>> All() => Task.FromResult(Store);
    }

    private sealed class FakeApplicationState : IApplicationState
    {
        public List<TrueApiToken> Tokens { get; } = [];

        public void DbStateUpdate(bool isOnline) { }
        public bool DbState() => true;
        public void UpdateNeedRestart(bool need) { }
        public bool NeedRestart() => false;
        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil) { }
        public TrueApiToken TrueApiToken(string inn) => new();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => Tokens;
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }
}
