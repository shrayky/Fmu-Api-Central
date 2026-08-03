using Domain.Bot;
using Domain.Configuration.Options;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Messages.Services;

public class AlertsConstuctor : IAlertMessageConstructor
{
    private readonly ILogger<AlertsConstuctor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageService _messageService;

    public AlertsConstuctor(ILogger<AlertsConstuctor> logger, IServiceScopeFactory scopeFactory, IMessageService messageService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _messageService = messageService;
    }

    public async Task<bool> SendNodesStatus(TelegramBotSetting bot)
    {
        _logger.LogInformation("Готовлю информацию для отправки в бот");

        using var scope = _scopeFactory.CreateScope();
        var instanceManager = scope.ServiceProvider.GetRequiredService<IInstanceManagerService>();
        var nodesResult = await instanceManager.All();

        if (nodesResult.IsFailure)
            _logger.LogError("Сообщения в бот: ошибка получения узлов fmu-api: {ex}", nodesResult.Error);

        var nodes = nodesResult.Value;
        var offlineThreshold = DateTime.Now.AddHours(bot.OfflineNodeAlertInterval * -1);
        var offlineNodes = nodes.Where(p => p.LastUpdated < offlineThreshold).ToList();
        var onlineNodes = nodes.Where(p => p.LastUpdated >= offlineThreshold).ToList();

        List<string> messages = [];

        // Для offline-узлов отправляем только уведомление о недоступности
        messages.AddRange(CheckOnlineNodes(offlineNodes));
        messages.AddRange(CheckLmStatus(onlineNodes));
        messages.AddRange(CheckLmVersions(onlineNodes, bot));
        messages.AddRange(CheckLmSyncDate(onlineNodes, bot));
        messages.AddRange(CheckTsPiotStatus(onlineNodes, bot));
        messages.AddRange(CheckTsPiotLicense(onlineNodes, bot));
        messages.AddRange(CheckTsPiotVersions(onlineNodes, bot));

        foreach (var message in messages)
        {
            var sendResult = await _messageService.Send(bot.BotToken, bot.ChatId, message);

            if (sendResult.IsFailure)
                _logger.LogError("Сообщения в бот: не удалось отправить сообщение {message} боту: {err}!",
                    message, sendResult.Error);
        }

        return true;
    }

    /// <summary>
    /// Формирует сообщения о недоступных узлах.
    /// </summary>
    private static List<string> CheckOnlineNodes(List<InstanceMonitoringInformation> offlineNodes)
    {
        List<string> messages = [];

        foreach (var node in offlineNodes)
        {
            var messageToChat = $"🚨<b>{node.Name}</b> Не в сети!%0A последний обмен: <u>{node.LastUpdated}</u>!";
            messages.Add(messageToChat);
        }

        return messages;
    }

    /// <summary>
    /// Формирует сообщения о локальных модулях в нерабочем статусе.
    /// </summary>
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
            var messageToChat = $"🚨<b>Локальный модуль в {lm.NodeName} {lm.ModuleAddress}</b>%0A в не рабочем состоянии!%0A статус: <u>{status}</u>!";

            messages.Add(messageToChat);
        }

        return messages;
    }

    /// <summary>
    /// Формирует сообщения об устаревших версиях локальных модулей.
    /// </summary>
    private static List<string> CheckLmVersions(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        List<string> messages = [];

        var versionAlert = bot.LocalModuleAlerts.VersionAlert;
        if (string.IsNullOrEmpty(versionAlert))
            return messages;

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
            if (!IsVersionBelowThreshold(lm.ModuleVersion, versionAlert))
                continue;

            var messageToChat = $"🚨<b>Локальный модуль в {lm.NodeName} {lm.ModuleAddress}</b> устарел!%0A текущая версия: <u>{lm.ModuleVersion}</u>!";

            messages.Add(messageToChat);
        }

        return messages;
    }

    /// <summary>
    /// Формирует сообщения о давно не синхронизированных локальных модулях.
    /// </summary>
    private static List<string> CheckLmSyncDate(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        var toDateTimestamp = DateTimeOffset.Now.AddDays(bot.LocalModuleAlerts.DaysWithoutSynchronization * -1).ToUnixTimeMilliseconds();
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
            var messageToChat = $"🚨<b>Локальный модуль в {lm.NodeName} {lm.ModuleAddress}</b> давно не обновлялся!%0A последнее обновление: <u>{DateTimeOffset.FromUnixTimeMilliseconds(lm.ModuleLastSync).ToLocalTime()}</u>%0AПроведите инициализацию!";
            messages.Add(messageToChat);
        }

        return messages;
    }

    /// <summary>
    /// Формирует сообщения о ТС ПИоТ в состоянии offline.
    /// </summary>
    private static List<string> CheckTsPiotStatus(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        List<string> messages = [];

        if (!bot.TsPiotAlerts.StatusAlertEnabled)
            return messages;

        var offlineTsPiots = nodes
            .SelectMany(n => n.TsPiots
                .Where(ts => !ts.Online)
                .Select(ts => new {
                    NodeName = n.Name,
                    TsName = ts.Name,
                    TsAddress = ts.Address,
                }))
            .ToList();

        foreach (var ts in offlineTsPiots)
        {
            var messageToChat = $"🚨<b>ТС ПИоТ {ts.TsName} в {ts.NodeName} {ts.TsAddress}</b>%0A не в сети!";
            messages.Add(messageToChat);
        }

        return messages;
    }

    /// <summary>
    /// Формирует сообщения об истекающих или истёкших лицензиях ТС ПИоТ.
    /// </summary>
    private static List<string> CheckTsPiotLicense(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        List<string> messages = [];

        if (!bot.TsPiotAlerts.LicenseAlertEnabled)
            return messages;

        var today = DateTime.Today;
        var alertUntil = today.AddDays(bot.TsPiotAlerts.LicenseAlertDays);

        var licenses = nodes
            .SelectMany(n => n.TsPiots
                .Where(ts => ts.LicenseActiveTill.HasValue)
                .Select(ts => new {
                    NodeName = n.Name,
                    TsName = ts.Name,
                    TsAddress = ts.Address,
                    LicenseDate = ts.LicenseActiveTill!.Value.Date,
                }))
            .ToList();

        foreach (var ts in licenses)
        {
            if (ts.LicenseDate < today)
            {
                var messageToChat = $"🚨<b>ТС ПИоТ {ts.TsName} в {ts.NodeName} {ts.TsAddress}</b>%0A Лицензия истекла!%0A дата: <u>{ts.LicenseDate:dd.MM.yyyy}</u>!";
                messages.Add(messageToChat);
                continue;
            }

            if (ts.LicenseDate > alertUntil)
                continue;

            var daysLeft = (ts.LicenseDate - today).Days;
            var messageExpiring = $"🚨<b>ТС ПИоТ {ts.TsName} в {ts.NodeName} {ts.TsAddress}</b>%0A Лицензия истекает через {daysLeft} дней!%0A дата: <u>{ts.LicenseDate:dd.MM.yyyy}</u>!";
            messages.Add(messageExpiring);
        }

        return messages;
    }

    /// <summary>
    /// Формирует сообщения об устаревших версиях ТС ПИоТ.
    /// </summary>
    private static List<string> CheckTsPiotVersions(List<InstanceMonitoringInformation> nodes, TelegramBotSetting bot)
    {
        List<string> messages = [];

        var versionAlert = bot.TsPiotAlerts.VersionAlert;
        if (string.IsNullOrEmpty(versionAlert))
            return messages;

        var versions = nodes
            .SelectMany(n => n.TsPiots
                .Select(ts => new {
                    NodeName = n.Name,
                    TsName = ts.Name,
                    TsAddress = ts.Address,
                    TsVersion = ts.Version,
                }))
            .ToList();

        foreach (var ts in versions)
        {
            if (!IsVersionBelowThreshold(ts.TsVersion, versionAlert))
                continue;

            var messageToChat = $"🚨<b>ТС ПИоТ {ts.TsName} в {ts.NodeName} {ts.TsAddress}</b> устарел!%0A текущая версия: <u>{ts.TsVersion}</u>!";
            messages.Add(messageToChat);
        }

        return messages;
    }

    /// <summary>
    /// Проверяет, что текущая версия ниже пороговой (суффикс после '-' игнорируется).
    /// </summary>
    private static bool IsVersionBelowThreshold(string currentVersion, string thresholdVersion)
    {
        if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(thresholdVersion))
            return false;

        var cleanCurrent = currentVersion.Split('-')[0];
        var cleanThreshold = thresholdVersion.Split('-')[0];

        return new Version(cleanCurrent) < new Version(cleanThreshold);
    }
}
