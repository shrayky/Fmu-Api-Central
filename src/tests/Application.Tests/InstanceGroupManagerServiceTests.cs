using Application.InstanceGroup.Services;
using Application.Tests.Fakes;
using CSharpFunctionalExtensions;
using Domain.Bot;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.InstanceGroup.Dto;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SettingsSchema;
using Domain.Entitys.SettingsSchema.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;

namespace Application.Tests;

public class InstanceGroupManagerServiceTests
{
    [Fact]
    public async Task Create_включённый_канал_без_chatId_отказывает()
    {
        var sut = CreateSut(new FakeGroupRepository());
        var result = await sut.Create(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = new AlertChannel { IsEnabled = true, ChatId = 0, BotToken = "t" }
        });

        Assert.True(result.IsFailure);
        Assert.Equal("ID чата не может быть 0", result.Error);
    }

    [Fact]
    public async Task Create_выключенный_канал_с_нулевым_chatId_пишет()
    {
        var repo = new FakeGroupRepository();
        var sut = CreateSut(repo);

        var result = await sut.Create(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = new AlertChannel { IsEnabled = false, ChatId = 0 }
        });

        Assert.True(result.IsSuccess);
        Assert.False(repo.Store["g1"].AlertChannel.IsEnabled);
        Assert.Equal(0, repo.Store["g1"].AlertChannel.ChatId);
    }

    [Fact]
    public async Task Update_включённый_канал_без_chatId_отказывает()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);

        var result = await sut.Update(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = new AlertChannel { IsEnabled = true, ChatId = 0 }
        });

        Assert.True(result.IsFailure);
        Assert.False(repo.Store["g1"].AlertChannel.IsEnabled);
    }

    [Fact]
    public async Task Create_null_канал_пишет_выключенный()
    {
        var repo = new FakeGroupRepository();
        var sut = CreateSut(repo);

        var result = await sut.Create(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = null!
        });

        Assert.True(result.IsSuccess);
        Assert.False(repo.Store["g1"].AlertChannel.IsEnabled);
    }

    [Fact]
    public async Task Update_null_канал_пишет_выключенный()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = new AlertChannel { IsEnabled = true, ChatId = 5 }
        };
        var sut = CreateSut(repo);

        var result = await sut.Update(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = null!
        });

        Assert.True(result.IsSuccess);
        Assert.False(repo.Store["g1"].AlertChannel.IsEnabled);
    }

    [Fact]
    public async Task Create_пишет_канал()
    {
        var repo = new FakeGroupRepository();
        var sut = CreateSut(repo);

        var result = await sut.Create(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            AlertChannel = new AlertChannel
            {
                IsEnabled = true,
                Provider = BotProvidersEnum.ntfy,
                ChatId = 7,
                BotToken = "tok"
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(7, repo.Store["g1"].AlertChannel.ChatId);
        Assert.Equal(BotProvidersEnum.ntfy, repo.Store["g1"].AlertChannel.Provider);
    }

    [Fact]
    public async Task TestChannel_включённый_шлёт_в_поля_тела()
    {
        var sender = new FakeMessageService("tg");
        var factory = new FakeMessageServiceFactory { Services = { [BotProvidersEnum.telegram] = sender } };
        var sut = CreateSut(new FakeGroupRepository(), factory);

        var result = await sut.TestChannel(new AlertChannel
        {
            IsEnabled = true,
            Provider = BotProvidersEnum.telegram,
            ChatId = 15,
            BotToken = "tok"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("tok", sender.LastToken);
        Assert.Equal(15, sender.LastChatId);
        Assert.Equal("Халло, мир!%0AСЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫМ!", sender.LastMessage);
    }

    [Fact]
    public async Task TestChannel_выключенный_не_шлёт()
    {
        var sender = new FakeMessageService("tg");
        var factory = new FakeMessageServiceFactory { Services = { [BotProvidersEnum.telegram] = sender } };
        var sut = CreateSut(new FakeGroupRepository(), factory);

        var result = await sut.TestChannel(new AlertChannel { IsEnabled = false, ChatId = 15 });

        Assert.True(result.IsFailure);
        Assert.Equal("Бот не подключен", result.Error);
        Assert.Null(sender.LastMessage);
    }

    [Fact]
    public async Task TestChannel_нулевой_chatId_не_шлёт()
    {
        var sender = new FakeMessageService("tg");
        var factory = new FakeMessageServiceFactory { Services = { [BotProvidersEnum.telegram] = sender } };
        var sut = CreateSut(new FakeGroupRepository(), factory);

        var result = await sut.TestChannel(new AlertChannel { IsEnabled = true, ChatId = 0, BotToken = "t" });

        Assert.True(result.IsFailure);
        Assert.Equal("ID чата не может быть 0", result.Error);
        Assert.Null(sender.LastMessage);
    }

    private static InstanceGroupManagerService CreateSut(
        FakeGroupRepository repository,
        IMessageServiceFactory? messageServiceFactory = null)
        => new(
            repository,
            new UnusedInstanceRepository(),
            new UnusedInstanceManagerService(),
            new UnusedSettingsSchemaRepository(),
            new UnusedParametersService(),
            messageServiceFactory ?? new FakeMessageServiceFactory());

    private sealed class FakeGroupRepository : IInstanceGroupRepository
    {
        public Dictionary<string, InstanceGroupEntity> Store { get; } = new();

        public Task<Result> Create(InstanceGroupEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result> Update(InstanceGroupEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<InstanceGroupEntity>> GetById(string id)
            => Task.FromResult(Store.TryGetValue(id, out var entity)
                ? Result.Success(entity)
                : Result.Failure<InstanceGroupEntity>($"Группа {id} не найдена"));

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> All() => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> ByListId(List<string> ids) => throw new NotImplementedException();

        public Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
            => throw new NotImplementedException();
    }

    private sealed class UnusedInstanceRepository : IInstanceRepository
    {
        public Task<Result> Update(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<InstanceEntity>> ByToken(string token) => throw new NotImplementedException();

        public Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, InstanceListFilter filter)
            => throw new NotImplementedException();

        public Task<Result<bool>> CreateInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<bool>> DeleteInstance(InstanceEntity instance) => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate)
            => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> All() => throw new NotImplementedException();

        public Task<Result<List<InstanceEntity>>> ByGroupIds(IReadOnlyList<string> groupIds)
            => throw new NotImplementedException();

        public Task<Result> ClearGroupLink(string groupId) => throw new NotImplementedException();
    }

    private sealed class UnusedInstanceManagerService : IInstanceManagerService
    {
        public Task<Result<FmuApiCentralResponse>> UpdateFmuApiInstanceInformation(
            string instanceData, bool markLegacyAccess = false)
            => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(
            int pageNumber, int pageSize, InstanceListFilter filter)
            => throw new NotImplementedException();

        public Task<bool> CreateNew(InstanceMonitoringInformation instanceInformation)
            => throw new NotImplementedException();

        public Task<bool> Delete(string instance) => throw new NotImplementedException();

        public Task<string> InstanceSettings(string token) => throw new NotImplementedException();

        public Task<Result> SettingsUploaded(string token) => throw new NotImplementedException();

        public Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdate(string token, long? rangeFrom)
            => throw new NotImplementedException();

        public Task<Result<ForceUpdateResult>> AssignForcedUpdate(IReadOnlyList<string> tokens, string updateId)
            => throw new NotImplementedException();

        public Task<Result<List<InstanceMonitoringInformation>>> OfflineInstance(DateTime toDate)
            => throw new NotImplementedException();

        public Task<Result<List<InstanceMonitoringInformation>>> All() => throw new NotImplementedException();

        public Task MarkLegacyAccess(string token) => throw new NotImplementedException();
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
