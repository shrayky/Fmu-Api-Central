using Application.Instance.DTO;
using Application.Instance.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramBot.Services;

namespace TelegramBot.Workers;

public class TelegramMessagingWorker : BackgroundService
{
    private readonly ILogger<TelegramMessagingWorker> _logger;
    private readonly IParametersService _settings;
    private readonly MessageService _messageService;

    private readonly IInstanceManagerService  _instanceManager;
    private const int StartDelayMinutes = 1;

    public TelegramMessagingWorker(ILogger<TelegramMessagingWorker> logger, IParametersService settings, MessageService messageService, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _settings = settings;
        _messageService = messageService;

        _instanceManager = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IInstanceManagerService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(StartDelayMinutes), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await _settings.Current().ConfigureAwait(false);
            var bot = settings.BotSettings;

            if (bot.IsEnabled)
            {
                _logger.LogInformation("Готовлю информацию для отправки в telegram");

                var nodesResult = await _instanceManager.All();

                if (nodesResult.IsFailure)
                    _logger.LogError("Телеграмм бот: ошибка получения узлов fmu-api: {ex}", nodesResult.Error);

                var nodes = nodesResult.Value;

                List<string> messages = [];

                var offlineNodes = CheckOnlineNodes(nodes, bot);
                var lmWithBadStatus = CheckLmStatus(nodes);
                var lmBadVersions = CheckLmVersions(nodes, bot);
                var lmBadSyncDate = CheckLmSyncDate(nodes, bot);

                messages.AddRange(offlineNodes);
                messages.AddRange(lmWithBadStatus);
                messages.AddRange(lmBadVersions);
                messages.AddRange(lmBadSyncDate);

                foreach (var message in messages)
                {
                    var sendResult = await _messageService.Send(bot.BotToken, bot.ChatId, message);

                    if (sendResult.IsFailure)
                        _logger.LogError("Телеграмм бот: не удалось отправить сообщение {message} боту: {err}!",
                            message, sendResult.Error);
                }
            }
            
            await Task.Delay(TimeSpan.FromHours(bot.AlertIntervalInMinutes), stoppingToken);
        }
    }

    private static List<string> CheckOnlineNodes(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        var toDate = DateTime.Now.AddHours(bot.OfflineNodeAlertInterval * -1);
        List<string> messages = [];
        
        var offlineNodes = nodes.Where(p => p.LastUpdated < toDate);
                
        foreach (var node in offlineNodes)
        {
            var messageToChat = $"🚨<b>{node.Name}</b> Не в сети!%0A последний обмен: <u>{node.LastUpdated}</u>!";
            messages.Add(messageToChat);
        }

        return messages;
    }

    private static List<string> CheckLmStatus(List<InstanceMonitoringInformation> nodes)
    {
        List<string> messages = [];
        
        var lmWithBadStatus = nodes
            .SelectMany(n => n.LocalModules
                .Where(lm => lm.Status != "ready")
                .Select(lm => new { 
                    NodeName = n.Name, 
                    ModuleAddress = lm.Address,
                    ModuleStatus = lm.Status,
                }))
            .ToList();
                
        foreach (var lm in lmWithBadStatus)
        {
            var status = lm.ModuleStatus == "" ? "не готов" : lm.ModuleStatus;
            var messageToChat = $"🚨<b>{lm.NodeName} {lm.ModuleAddress}</b> в не рабочем состоянии!%0A статус: <u>{status}</u>!";
                    
            messages.Add(messageToChat);
        }
        
        return messages;
    }
    private List<string> CheckLmVersions(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        List<string> messages = [];
        
        var lmVersion = nodes
            .SelectMany(n => n.LocalModules
                .Where(lm => lm.Status == "ready")
                .Select(lm => new { 
                    NodeName = n.Name, 
                    ModuleAddress = lm.Address,
                    ModuleVersion = lm.Version,
                }))
            .ToList();
                
        foreach (var lm in lmVersion)
        {
            var currentVersion = lm.ModuleVersion.Split('-');
            var cleanCurrentVersion = currentVersion[0];
                    
            var isVersionOutdated = !string.IsNullOrEmpty(lm.ModuleVersion) && 
                                    !string.IsNullOrEmpty(bot.LocalModuleVersionAlert) &&
                                    new Version(cleanCurrentVersion) < new Version(bot.LocalModuleVersionAlert);
    
            if (!isVersionOutdated)
                continue;
                    
            var messageToChat = $"🚨<b>{lm.NodeName} {lm.ModuleAddress}</b> устарел!%0A текущая версия: <u>{lm.ModuleVersion}</u>!";
            
            messages.Add(messageToChat);
                    
        }
        
        return messages;
    }

    private static List<string> CheckLmSyncDate(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        var toDateTimestamp = DateTimeOffset.Now.AddDays(bot.LocalModuleDaysWithoutSynchronization * -1).ToUnixTimeMilliseconds();
        List<string> messages = [];
        
        var lmSyncDateTime = nodes
            .SelectMany(n => n.LocalModules
                .Where(lm => lm.Status == "ready" && lm.LastSync < toDateTimestamp)
                .Select(lm => new { 
                    NodeName = n.Name, 
                    ModuleAddress = lm.Address,
                    ModuleLastSync = lm.LastSync,
                }))
            .ToList();
                
        foreach (var lm in lmSyncDateTime)
        {
            var messageToChat = $"🚨<b>{lm.NodeName} {lm.ModuleAddress}</b> давно не обновлялся!%0A последнее обновление: <u>{DateTimeOffset.FromUnixTimeMilliseconds(lm.ModuleLastSync).ToLocalTime()}</u>!";
            messages.Add(messageToChat);
        }

        return messages;
    }
}