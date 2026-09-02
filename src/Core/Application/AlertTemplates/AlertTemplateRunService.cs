using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Bot;
using Domain.Configuration.Interfaces;
using Domain.Entitys.AlertTemplates.Dto;
using Domain.Entitys.AlertTemplates.Interfaces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.AlertTemplates;

[AutoRegisterService]
public class AlertTemplateRunService : IAlertTemplateRunService
{
    private const int StatisticsLookbackDays = 30;

    private readonly ILogger<AlertTemplateRunService> _logger;
    private readonly IAlertTemplateRepository _templates;
    private readonly IAlertTemplateManager _manager;
    private readonly IAlertDatasetScriptExecutor _executor;
    private readonly IInstanceRepository _instances;
    private readonly IMarksCheckStatisticRepository _statistics;
    private readonly IParametersService _parameters;
    private readonly IMessageService _messageService;

    public AlertTemplateRunService(
        ILogger<AlertTemplateRunService> logger,
        IAlertTemplateRepository templates,
        IAlertTemplateManager manager,
        IAlertDatasetScriptExecutor executor,
        IInstanceRepository instances,
        IMarksCheckStatisticRepository statistics,
        IParametersService parameters,
        IMessageService messageService)
    {
        _logger = logger;
        _templates = templates;
        _manager = manager;
        _executor = executor;
        _instances = instances;
        _statistics = statistics;
        _parameters = parameters;
        _messageService = messageService;
    }

    public async Task<Result> RunDueTemplates(DateTime now)
    {
        await _manager.EnsureDefaults();

        var due = (await _templates.AllEnabled())
            .Where(template => template.IsDueAt(now))
            .ToList();

        if (due.Count == 0)
            return Result.Success();

        var parameters = await _parameters.Current();
        var bot = parameters.BotSettings;
        if (!bot.IsEnabled)
            return Result.Success();

        var contextResult = await BuildContext();
        if (contextResult.IsFailure)
            return Result.Failure(contextResult.Error);

        var context = contextResult.Value;

        foreach (var template in due)
        {
            var executeResult = _executor.Execute(template.Script, context);
            if (executeResult.IsFailure)
            {
                _logger.LogError(
                    "Шаблон оповещения {Name} ({Id}) завершился ошибкой: {Error}",
                    template.Name, template.Id, executeResult.Error);
                continue;
            }

            var dataset = executeResult.Value;
            if (!dataset.HasContent)
                continue;

            var messages = MessagesToSend(dataset, template.Name);
            foreach (var message in messages)
            {
                var sendResult = await _messageService.Send(
                    bot.BotToken,
                    bot.ChatId,
                    AlertMessageFormatter.ToTelegramText(message));

                if (sendResult.IsFailure)
                {
                    _logger.LogError(
                        "Не удалось отправить оповещение шаблона {Name}: {Error}",
                        template.Name, sendResult.Error);
                }
            }
        }

        return Result.Success();
    }

    public async Task<Result<AlertDatasetResult>> Preview(string script)
    {
        await _manager.EnsureDefaults();

        var contextResult = await BuildContext();
        if (contextResult.IsFailure)
            return Result.Failure<AlertDatasetResult>(contextResult.Error);

        var executeResult = _executor.Execute(script, contextResult.Value);
        if (executeResult.IsFailure)
            return executeResult;

        return Result.Success(WithFormattedMessage(executeResult.Value, "Просмотр"));
    }

    private async Task<Result<AlertDatasetContext>> BuildContext()
    {
        var instancesResult = await _instances.All();
        if (instancesResult.IsFailure)
            return Result.Failure<AlertDatasetContext>(instancesResult.Error);

        var now = DateTimeOffset.Now;
        var instances = instancesResult.Value
            .Select(entity => ToInstanceSnapshot(entity, now))
            .ToList();
        var names = instances.ToDictionary(instance => instance.Id, instance => instance.Name);

        var statisticsResult = await _statistics.GetByDateRange(
            now.Date.AddDays(-StatisticsLookbackDays),
            now.Date);

        var statistics = statisticsResult.IsSuccess
            ? statisticsResult.Value.Select(entity => ToStatisticSnapshot(entity, names)).ToList()
            : [];

        if (statisticsResult.IsFailure)
            _logger.LogWarning("Статистика проверок для шаблонов недоступна: {Error}", statisticsResult.Error);

        var bot = (await _parameters.Current()).BotSettings;

        return Result.Success(new AlertDatasetContext
        {
            Now = now,
            Instances = instances,
            Statistics = statistics,
            Settings = new AlertSettingsSnapshot
            {
                OfflineNodeAlertInterval = bot.OfflineNodeAlertInterval,
                LocalModuleAlerts = new AlertLocalModuleSettingsSnapshot
                {
                    VersionAlert = bot.LocalModuleAlerts.VersionAlert,
                    DaysWithoutSynchronization = bot.LocalModuleAlerts.DaysWithoutSynchronization
                },
                TsPiotAlerts = new AlertTsPiotSettingsSnapshot
                {
                    StatusAlertEnabled = bot.TsPiotAlerts.StatusAlertEnabled,
                    LicenseAlertEnabled = bot.TsPiotAlerts.LicenseAlertEnabled,
                    LicenseAlertDays = bot.TsPiotAlerts.LicenseAlertDays,
                    VersionAlert = bot.TsPiotAlerts.VersionAlert
                }
            }
        });
    }

    /// <summary>
    /// Элементы набора уходят отдельными сообщениями, как в AlertsConstuctor.
    /// </summary>
    private static List<string> MessagesToSend(AlertDatasetResult dataset, string fallbackTitle)
    {
        if (dataset.Items.Count > 0)
            return dataset.Items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();

        var formatted = AlertMessageFormatter.Format(dataset, fallbackTitle);
        return string.IsNullOrWhiteSpace(formatted) ? [] : [formatted];
    }

    private static AlertDatasetResult WithFormattedMessage(AlertDatasetResult dataset, string fallbackTitle)
    {
        if (!dataset.HasContent)
            return dataset;

        return dataset with { Message = AlertMessageFormatter.Format(dataset, fallbackTitle) };
    }

    private static AlertInstanceSnapshot ToInstanceSnapshot(Domain.Entitys.Instance.InstanceEntity entity, DateTimeOffset now)
    {
        var hoursSinceUpdate = Math.Max(0, (now.LocalDateTime - entity.UpdatedAt).TotalHours);

        return new AlertInstanceSnapshot
        {
            Id = entity.Id,
            Name = entity.Name,
            Address = entity.Address,
            Version = $"{entity.Settings.Version}.{entity.Settings.Assembly}",
            LastUpdated = entity.UpdatedAt.ToString("G"),
            HoursSinceUpdate = Math.Round(hoursSinceUpdate, 2),
            LocalModules = entity.LocalModules.Select(lm => new AlertLocalModuleSnapshot
            {
                Id = lm.Id,
                Address = lm.Address,
                Version = lm.Version,
                LastSync = lm.LastSync,
                Status = lm.Status,
                OperationMode = lm.OperationMode
            }).ToList(),
            TsPiots = entity.TsPiots.Select(ts => new AlertTsPiotSnapshot
            {
                Name = ts.Name,
                Address = ts.Address,
                Online = ts.Online,
                Version = ts.Version,
                LicenseActiveTill = ts.LicenseActiveTill?.ToString("O")
            }).ToList()
        };
    }

    private static AlertStatisticSnapshot ToStatisticSnapshot(
        Domain.Entitys.MarksCheckStatistic.MarkCheckStatisticsEntity entity,
        IReadOnlyDictionary<string, string> names)
    {
        var instanceName = names.TryGetValue(entity.NodeId, out var name) ? name : string.Empty;

        return new AlertStatisticSnapshot
        {
            NodeId = entity.NodeId,
            InstanceName = instanceName,
            Date = entity.Date,
            DateIso = ToDateIso(entity.Date),
            Total = entity.Total,
            SuccessfulOnlineChecks = entity.SuccessfulOnlineChecks,
            SuccessfulOfflineChecks = entity.SuccessfulOfflineChecks,
            SuccessRatePercentage = entity.SuccessRatePercentage
        };
    }

    /// <summary>
    /// Преобразует unix-секунды или миллисекунды в ISO-дату.
    /// </summary>
    private static string ToDateIso(long unixValue)
    {
        var milliseconds = unixValue > 1_000_000_000_000 ? unixValue : unixValue * 1000;
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("O");
    }
}
