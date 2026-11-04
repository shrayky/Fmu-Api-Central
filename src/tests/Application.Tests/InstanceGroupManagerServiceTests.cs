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

    /// <summary>
    /// В группу пишется список организаций для выгрузки токенов.
    /// </summary>
    [Fact]
    public async Task Create_пишет_организации()
    {
        var repo = new FakeGroupRepository();
        var sut = CreateSut(repo);

        var result = await sut.Create(new InstanceGroupView
        {
            Id = "g1",
            Name = "Группа",
            OrganizationIds = ["o1", "o2"]
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(["o1", "o2"], repo.Store["g1"].OrganizationIds);
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

    [Fact]
    public async Task SaveCheckerSettings_пишет_объект()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);
        var body = """{"serverConfig":{"apiIpPort":2599}}"""u8.ToArray();

        var result = await sut.SaveCheckerSettings("g1", body);

        Assert.True(result.IsSuccess);
        var read = await repo.ReadCheckerSettings("g1");
        Assert.True(read.Value.Exists);
        Assert.Equal(body, read.Value.Content);
    }

    [Fact]
    public async Task SaveCheckerSettings_битый_файл_не_пишет()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);

        var result = await sut.SaveCheckerSettings("g1", "{"u8.ToArray());

        Assert.True(result.IsFailure);
        Assert.Equal("Файл настроек должен быть JSON-объектом", result.Error);
        Assert.False((await repo.ReadCheckerSettings("g1")).Value.Exists);
    }

    [Fact]
    public async Task SaveCheckerSettings_файл_больше_2_мегабайт_не_пишет()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);
        var body = new byte[2 * 1024 * 1024 + 1];

        var result = await sut.SaveCheckerSettings("g1", body);

        Assert.True(result.IsFailure);
        Assert.Equal("Файл настроек больше 2 МБ", result.Error);
        Assert.False((await repo.ReadCheckerSettings("g1")).Value.Exists);
    }

    [Fact]
    public async Task SaveCheckerSettings_недоступность_базы_не_называет_группу_пропавшей()
    {
        var repo = new FakeGroupRepository { GetByIdError = "БД недоступна сейчас" };
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);

        var result = await sut.SaveCheckerSettings("g1", """{"a":1}"""u8.ToArray());

        Assert.True(result.IsFailure);
        Assert.Equal("БД недоступна сейчас", result.Error);
        Assert.False((await repo.ReadCheckerSettings("g1")).Value.Exists);
    }

    [Fact]
    public async Task SaveCheckerSettings_нет_группы()
    {
        var sut = CreateSut(new FakeGroupRepository());

        var result = await sut.SaveCheckerSettings("missing", """{"a":1}"""u8.ToArray());

        Assert.True(result.IsFailure);
        Assert.Equal("Группа не найдена", result.Error);
    }

    [Fact]
    public async Task SaveCheckerSettings_замена_перезаписывает_байты()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);
        await sut.SaveCheckerSettings("g1", """{"a":1}"""u8.ToArray());
        var next = """{"a":2}"""u8.ToArray();

        Assert.True((await sut.SaveCheckerSettings("g1", next)).IsSuccess);
        Assert.Equal(next, (await repo.ReadCheckerSettings("g1")).Value.Content);
    }

    [Fact]
    public async Task Create_не_стирает_файл_настроек()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);
        var body = """{"a":1}"""u8.ToArray();
        Assert.True((await sut.SaveCheckerSettings("g1", body)).IsSuccess);

        var saved = await sut.Create(new InstanceGroupView { Id = "g1", Name = "Новое имя" });

        Assert.True(saved.IsSuccess);
        Assert.Equal("Новое имя", repo.Store["g1"].Name);
        Assert.Equal(body, (await repo.ReadCheckerSettings("g1")).Value.Content);
    }

    [Fact]
    public async Task DeleteCheckerSettings_снимает_и_повтор_не_ошибка()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);
        await sut.SaveCheckerSettings("g1", """{"a":1}"""u8.ToArray());

        Assert.True((await sut.DeleteCheckerSettings("g1")).IsSuccess);
        Assert.False((await repo.ReadCheckerSettings("g1")).Value.Exists);
        Assert.True((await sut.DeleteCheckerSettings("g1")).IsSuccess);
    }

    [Fact]
    public async Task DeleteCheckerSettings_нет_группы()
    {
        var result = await CreateSut(new FakeGroupRepository()).DeleteCheckerSettings("missing");

        Assert.True(result.IsFailure);
        Assert.Equal("Группа не найдена", result.Error);
    }

    [Fact]
    public async Task List_отдаёт_признак_файла()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа", HasCheckerSettings = true };
        var sut = CreateSut(repo, parameters: new ListParameters());

        var page = await sut.List(1, 10);

        Assert.True(page.Content.Single().HasCheckerSettings);
    }

    [Fact]
    public async Task HasCheckerSettings_true_когда_файл_есть()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);
        Assert.True((await sut.SaveCheckerSettings("g1", """{"a":1}"""u8.ToArray())).IsSuccess);

        var result = await sut.HasCheckerSettings("g1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task HasCheckerSettings_false_когда_файла_нет()
    {
        var repo = new FakeGroupRepository();
        repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
        var sut = CreateSut(repo);

        var result = await sut.HasCheckerSettings("g1");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task HasCheckerSettings_нет_группы()
    {
        var result = await CreateSut(new FakeGroupRepository()).HasCheckerSettings("missing");

        Assert.True(result.IsFailure);
        Assert.Equal("Группа не найдена", result.Error);
    }

    private static InstanceGroupManagerService CreateSut(
        FakeGroupRepository repository,
        IMessageServiceFactory? messageServiceFactory = null,
        IParametersService? parameters = null)
        => new(
            repository,
            new UnusedInstanceRepository(),
            new UnusedInstanceManagerService(),
            new UnusedSettingsSchemaRepository(),
            parameters ?? new UnusedParametersService(),
            messageServiceFactory ?? new FakeMessageServiceFactory());

    private sealed class FakeGroupRepository : IInstanceGroupRepository
    {
        public Dictionary<string, InstanceGroupEntity> Store { get; } = new();
        public Dictionary<string, byte[]> Files { get; } = new();

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

        public string? GetByIdError { get; init; }

        public Task<Result<InstanceGroupEntity>> GetById(string id)
        {
            if (GetByIdError != null)
                return Task.FromResult(Result.Failure<InstanceGroupEntity>(GetByIdError));

            return Task.FromResult(Store.TryGetValue(id, out var entity)
                ? Result.Success(entity)
                : Result.Failure<InstanceGroupEntity>($"Группа {id} не найдена"));
        }

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
            => Task.FromResult(new PaginatedResponse<InstanceGroupEntity>
            {
                Content = Store.Values.ToList(),
                TotalCount = Store.Count,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                ListEnabled = true
            });

        public Task<Result> SaveCheckerSettings(string groupId, byte[] content)
        {
            Files[groupId] = content.ToArray();
            return Task.FromResult(Result.Success());
        }

        public Task<Result> DeleteCheckerSettings(string groupId)
        {
            Files.Remove(groupId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<CheckerSettingsRead>> ReadCheckerSettings(string groupId)
        {
            if (!Files.TryGetValue(groupId, out var content))
                return Task.FromResult(Result.Success(new CheckerSettingsRead(false, [])));

            return Task.FromResult(Result.Success(new CheckerSettingsRead(true, content)));
        }

        public Task<Result<bool>> HasCheckerSettings(string groupId)
            => Task.FromResult(Result.Success(Files.ContainsKey(groupId)));

        public Task<List<InstanceGroupEntity>> All() => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> ByListId(List<string> ids) => throw new NotImplementedException();

        public Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
            => throw new NotImplementedException();

        public Task<Result> ClearOrganizationLink(string organizationId)
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

        public Task<Result<List<InstanceEntity>>> All()
            => Task.FromResult(Result.Success(new List<InstanceEntity>()));

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

        public Task<Result> Delete(string instance) => throw new NotImplementedException();

        public Task<string> InstanceSettings(string token) => throw new NotImplementedException();

        public Task<Result> SettingsUploaded(string token) => throw new NotImplementedException();

        public Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdate(string token, long? rangeFrom)
            => throw new NotImplementedException();

        public Task<Result<ForceUpdateResult>> AssignForcedUpdate(IReadOnlyList<string> tokens, string updateId)
            => throw new NotImplementedException();

        public Task<Result<GroupAssignResult>> AssignGroup(IReadOnlyList<string> tokens, string groupId)
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

    private sealed class ListParameters : IParametersService
    {
        public Task<Parameters> Current() => Task.FromResult(new Parameters());
        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }
}
