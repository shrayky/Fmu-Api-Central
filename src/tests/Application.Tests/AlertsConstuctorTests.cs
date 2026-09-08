using Application.Tests.Fakes;
using CSharpFunctionalExtensions;
using Domain.Bot;
using Domain.Configuration.Options;
using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Dto.Responces;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.InstanceGroup.Dto;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Messages.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class AlertsConstuctorTests
{
    [Fact]
    public async Task SendNodesStatus_два_offline_узла_в_разных_группах_шлют_в_свои_каналы()
    {
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(sender, [
            OfflineNode("shop-a", "g1"),
            OfflineNode("shop-b", "g2")
        ], [
            Group("g1", chatId: 10),
            Group("g2", chatId: 20)
        ]);

        await sut.SendNodesStatus(GlobalBot());

        Assert.Contains(sender.Sends, send => send.ChatId == 10 && send.Message.Contains("shop-a"));
        Assert.Contains(sender.Sends, send => send.ChatId == 20 && send.Message.Contains("shop-b"));
        Assert.DoesNotContain(sender.Sends, send => send.ChatId == 99);
        Assert.Equal(2, sender.Sends.Count);
    }

    [Fact]
    public async Task SendNodesStatus_узел_без_группы_шлёт_в_глобальный_канал()
    {
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(sender, [OfflineNode("lonely", "")], []);

        await sut.SendNodesStatus(GlobalBot());

        Assert.Contains(sender.Sends, send => send.ChatId == 99 && send.Message.Contains("lonely"));
    }

    [Fact]
    public async Task SendNodesStatus_All_ошибка_не_шлёт_возвращает_false()
    {
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(
            sender,
            Result.Failure<List<InstanceMonitoringInformation>>("db down"),
            []);

        var result = await sut.SendNodesStatus(GlobalBot());

        Assert.False(result);
        Assert.Empty(sender.Sends);
    }

    [Fact]
    public async Task SendNodesStatus_узел_без_группы_глобальный_выключен_не_шлёт()
    {
        var sender = new FakeMessageService("tg");
        var sut = CreateSut(sender, [OfflineNode("lonely", "")], []);
        var bot = GlobalBot();
        bot.IsEnabled = false;

        await sut.SendNodesStatus(bot);

        Assert.Empty(sender.Sends);
    }

    private static AlertsConstuctor CreateSut(
        FakeMessageService sender,
        List<InstanceMonitoringInformation> nodes,
        List<InstanceGroupEntity> groups)
        => CreateSut(sender, Result.Success(nodes), groups);

    private static AlertsConstuctor CreateSut(
        FakeMessageService sender,
        Result<List<InstanceMonitoringInformation>> nodesResult,
        List<InstanceGroupEntity> groups)
    {
        var factory = new FakeMessageServiceFactory
        {
            Services = { [BotProvidersEnum.telegram] = sender }
        };

        return new AlertsConstuctor(
            NullLogger<AlertsConstuctor>.Instance,
            new FakeInstanceManagerService(nodesResult),
            new FakeGroupRepository(groups),
            factory);
    }

    private static TelegramBotSetting GlobalBot() => new()
    {
        IsEnabled = true,
        ChatId = 99,
        BotToken = "glb-tok",
        Provider = BotProvidersEnum.telegram,
        OfflineNodeAlertInterval = 1,
        LocalModuleAlerts = new LocalModuleAlertSettings
        {
            VersionAlert = string.Empty
        },
        TsPiotAlerts = new TsPiotAlertSettings
        {
            StatusAlertEnabled = false,
            LicenseAlertEnabled = false,
            VersionAlert = string.Empty
        }
    };

    private static InstanceMonitoringInformation OfflineNode(string name, string groupId) => new()
    {
        Name = name,
        LastUpdated = DateTime.Now.AddHours(-3),
        LocalModules = [],
        TsPiots = [],
        Group = string.IsNullOrEmpty(groupId)
            ? new GroupLink()
            : new GroupLink { Id = groupId, Name = groupId }
    };

    private static InstanceGroupEntity Group(string id, long chatId) => new()
    {
        Id = id,
        Name = id,
        AlertChannel = new AlertChannel
        {
            IsEnabled = true,
            ChatId = chatId,
            BotToken = $"{id}-tok",
            Provider = BotProvidersEnum.telegram
        }
    };

    private sealed class FakeInstanceManagerService : IInstanceManagerService
    {
        private readonly Result<List<InstanceMonitoringInformation>> _all;

        public FakeInstanceManagerService(Result<List<InstanceMonitoringInformation>> all)
        {
            _all = all;
        }

        public Task<Result<List<InstanceMonitoringInformation>>> All()
            => Task.FromResult(_all);

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

        public Task MarkLegacyAccess(string token) => throw new NotImplementedException();
    }

    private sealed class FakeGroupRepository : IInstanceGroupRepository
    {
        private readonly List<InstanceGroupEntity> _groups;

        public FakeGroupRepository(List<InstanceGroupEntity> groups)
        {
            _groups = groups;
        }

        public Task<List<InstanceGroupEntity>> All() => Task.FromResult(_groups);

        public Task<Result> Create(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result<InstanceGroupEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> ByListId(List<string> ids) => throw new NotImplementedException();

        public Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
            => throw new NotImplementedException();
    }
}
