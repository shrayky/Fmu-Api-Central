using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Bot;
using Domain.Configuration.Interfaces;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.AlertTemplates.Dto;
using Domain.Entitys.AlertTemplates.Interfaces;
using Domain.Entitys.Instance;
using Domain.Entitys.Interfaces;
using Domain.Configuration.Options;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Domain.Entitys.SettingsSchema;
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
    private readonly ICrptViolationsRepository _violations;
    private readonly IOrganizationRepository _organizations;
    private readonly IParametersService _parameters;
    private readonly IInstanceGroupRepository _groups;
    private readonly IMessageServiceFactory _messageServiceFactory;
    private readonly IDigitalSignatureService _signatures;
    private readonly ICryptoProLicenseService _licenses;

    public AlertTemplateRunService(
        ILogger<AlertTemplateRunService> logger,
        IAlertTemplateRepository templates,
        IAlertTemplateManager manager,
        IAlertDatasetScriptExecutor executor,
        IInstanceRepository instances,
        IMarksCheckStatisticRepository statistics,
        ICrptViolationsRepository violations,
        IOrganizationRepository organizations,
        IParametersService parameters,
        IInstanceGroupRepository groups,
        IMessageServiceFactory messageServiceFactory,
        IDigitalSignatureService signatures,
        ICryptoProLicenseService licenses)
    {
        _logger = logger;
        _templates = templates;
        _manager = manager;
        _executor = executor;
        _instances = instances;
        _statistics = statistics;
        _violations = violations;
        _organizations = organizations;
        _parameters = parameters;
        _groups = groups;
        _messageServiceFactory = messageServiceFactory;
        _signatures = signatures;
        _licenses = licenses;
    }

    public async Task<Result> RunDueTemplates(DateTime now)
    {
        await _manager.EnsureDefaults();

        var due = (await _templates.AllEnabled())
            .Where(template => template.IsDueAt(now))
            .ToList();

        if (due.Count == 0)
            return Result.Success();

        var instancesResult = await _instances.All();
        if (instancesResult.IsFailure)
            return Result.Failure(instancesResult.Error);

        var instances = instancesResult.Value;
        var groups = await _groups.All();
        var bot = (await _parameters.Current()).BotSettings;
        var global = AlertChannel.FromBotSettings(bot);
        var statistics = await LoadStatistics();
        var violations = await LoadViolations();
        var organizations = await LoadOrganizations();
        var license = LoadLicense();
        // Лицензия одна на машину службы: в общий канал, а если он выключен — в первую включённую группу.
        var licenseDelivered = global.IsEnabled;

        foreach (var group in groups)
        {
            var channel = group.AlertChannel ?? AlertChannel.Disabled();
            if (!channel.IsEnabled)
                continue;

            var groupLicense = AlertCryptoProLicenseSnapshot.None;
            if (!licenseDelivered)
            {
                groupLicense = license;
                licenseDelivered = true;
            }

            var groupInstances = instances.Where(instance => instance.GroupId == group.Id).ToList();
            var groupOrganizations = organizations
                .Where(organization => (group.OrganizationIds ?? []).Contains(organization.Id))
                .ToList();
            var sendResult = await RunTemplatesForChannel(
                due, groupInstances, channel, statistics, violations, groupOrganizations, groupLicense, bot);
            if (sendResult.IsFailure)
                return sendResult;
        }

        var enabledGroups = groups
            .Where(group => (group.AlertChannel ?? AlertChannel.Disabled()).IsEnabled)
            .ToList();
        var enabledGroupIds = enabledGroups.Select(group => group.Id).ToHashSet();
        var enabledOrganizationIds = enabledGroups
            .SelectMany(group => group.OrganizationIds ?? [])
            .ToHashSet();
        var fallback = instances
            .Where(instance => !enabledGroupIds.Contains(instance.GroupId))
            .ToList();
        var fallbackOrganizations = organizations
            .Where(organization => !enabledOrganizationIds.Contains(organization.Id))
            .ToList();

        if (global.IsEnabled)
            return await RunTemplatesForChannel(
                due, fallback, global, statistics, violations, fallbackOrganizations, license, bot);

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

    private async Task<Result> RunTemplatesForChannel(
        List<AlertTemplateEntity> due,
        List<InstanceEntity> instances,
        AlertChannel channel,
        IReadOnlyList<MarkCheckStatisticsEntity> statistics,
        IReadOnlyList<AlertViolationDaySnapshot> violations,
        IReadOnlyList<AlertOrganizationSnapshot> organizations,
        AlertCryptoProLicenseSnapshot license,
        TelegramBotSetting bot)
    {
        var contextResult = BuildContext(instances, statistics, violations, organizations, license, bot);
        if (contextResult.IsFailure)
            return Result.Failure(contextResult.Error);

        var senderResult = _messageServiceFactory.For(channel.Provider);
        if (senderResult.IsFailure)
        {
            _logger.LogError("Не удалось получить сервис сообщений: {Error}", senderResult.Error);
            return Result.Success();
        }

        foreach (var template in due)
        {
            var executeResult = _executor.Execute(template.Script, contextResult.Value);
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

            foreach (var message in MessagesToSend(dataset, template.Name))
            {
                var sendResult = await senderResult.Value.Send(
                    channel.BotToken,
                    channel.ChatId,
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

    private async Task<Result<AlertDatasetContext>> BuildContext()
    {
        var instancesResult = await _instances.All();
        if (instancesResult.IsFailure)
            return Result.Failure<AlertDatasetContext>(instancesResult.Error);

        var bot = (await _parameters.Current()).BotSettings;
        return BuildContext(
            instancesResult.Value,
            await LoadStatistics(),
            await LoadViolations(),
            await LoadOrganizations(),
            LoadLicense(),
            bot);
    }

    private Result<AlertDatasetContext> BuildContext(
        IReadOnlyList<InstanceEntity> entities,
        IReadOnlyList<MarkCheckStatisticsEntity> statisticsEntities,
        IReadOnlyList<AlertViolationDaySnapshot> violations,
        IReadOnlyList<AlertOrganizationSnapshot> organizations,
        AlertCryptoProLicenseSnapshot license,
        TelegramBotSetting bot)
    {
        var now = DateTimeOffset.Now;
        var instances = entities
            .Select(entity => ToInstanceSnapshot(entity, now))
            .ToList();
        var names = instances.ToDictionary(instance => instance.Id, instance => instance.Name);
        var instanceIds = names.Keys.ToHashSet();

        var statistics = statisticsEntities
            .Where(entity => instanceIds.Contains(entity.NodeId))
            .Select(entity => ToStatisticSnapshot(entity, names))
            .ToList();

        return Result.Success(new AlertDatasetContext
        {
            Now = now,
            Instances = instances,
            Statistics = statistics,
            Violations = violations,
            Organizations = organizations,
            CryptoProLicense = license,
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

    private async Task<IReadOnlyList<MarkCheckStatisticsEntity>> LoadStatistics()
    {
        var now = DateTimeOffset.Now;
        var statisticsResult = await _statistics.GetByDateRange(
            now.Date.AddDays(-StatisticsLookbackDays),
            now.Date);

        if (statisticsResult.IsFailure)
        {
            _logger.LogWarning("Статистика проверок для шаблонов недоступна: {Error}", statisticsResult.Error);
            return [];
        }

        return statisticsResult.Value;
    }

    private AlertCryptoProLicenseSnapshot LoadLicense()
    {
        var view = _licenses.View();
        if (view.IsFailure)
        {
            _logger.LogWarning("Лицензию КриптоПро прочитать не удалось: {Error}", view.Error);
            return AlertCryptoProLicenseSnapshot.None;
        }

        var parsed = CryptoProLicenseView.Parse(view.Value);
        return new AlertCryptoProLicenseSnapshot
        {
            Permanent = parsed.Permanent,
            ExpiresAt = parsed.ExpiresAt?.ToString("yyyy-MM-dd")
        };
    }

    private async Task<List<AlertOrganizationSnapshot>> LoadOrganizations()
    {
        var organizations = await _organizations.All();
        var byNumber = _signatures.ListIncludingExpired()
            .Where(certificate => !string.IsNullOrWhiteSpace(certificate.Number))
            .GroupBy(certificate => certificate.Number.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return organizations
            .Select(organization =>
            {
                var snapshot = ToOrganizationSnapshot(organization, byNumber);
                if (snapshot.CertificateNumber.Length > 0 && snapshot.CertificateWorkUntil == null)
                {
                    _logger.LogWarning(
                        "У организации {Name} ({Inn}) указан сертификат {Number}, в хранилище его нет",
                        snapshot.Name,
                        snapshot.Inn,
                        snapshot.CertificateNumber);
                }

                return snapshot;
            })
            .ToList();
    }

    private static AlertOrganizationSnapshot ToOrganizationSnapshot(
        OrganizationEntity organization,
        IReadOnlyDictionary<string, DigitalSignature> certificates)
    {
        var number = organization.TrueApiIntegrationSettings?.DigitalSignature?.Trim() ?? string.Empty;
        DigitalSignature? certificate = null;
        if (number.Length > 0)
            certificates.TryGetValue(number, out certificate);

        return new AlertOrganizationSnapshot
        {
            Id = organization.Id,
            Name = organization.Name,
            Inn = organization.Inn,
            CertificateNumber = number,
            CertificateWorkUntil = certificate?.WorkUntil.ToString("O")
        };
    }

    /// <summary>
    /// Берёт дни отклонений ЧЗ за тот же период, что и статистика проверок.
    /// </summary>
    private async Task<IReadOnlyList<AlertViolationDaySnapshot>> LoadViolations()
    {
        var now = DateTimeOffset.Now;
        var violationsResult = await _violations.GetByDateRange(
            now.Date.AddDays(-StatisticsLookbackDays),
            now.Date);

        if (violationsResult.IsFailure)
        {
            _logger.LogWarning("Отклонения ЧЗ для шаблонов недоступны: {Error}", violationsResult.Error);
            return [];
        }

        var organizations = await _organizations.All();
        var names = organizations
            .Where(item => !string.IsNullOrWhiteSpace(item.Inn))
            .GroupBy(item => item.Inn.Trim())
            .ToDictionary(group => group.Key, group => group.First().Name);

        return violationsResult.Value
            .Select(entity => ToViolationSnapshot(entity, names))
            .ToList();
    }

    private static AlertViolationDaySnapshot ToViolationSnapshot(
        CrptViolationsDailyEntity entity,
        IReadOnlyDictionary<string, string> names)
    {
        names.TryGetValue(entity.Inn, out var organizationName);

        return new AlertViolationDaySnapshot
        {
            Inn = entity.Inn,
            OrganizationName = organizationName ?? entity.Inn,
            Date = entity.Date,
            DateIso = ToDateIso(entity.Date),
            DateYmd = ToDateYmd(entity.Date),
            PenaltyAmountRub = entity.PenaltyAmountRub,
            Violations = entity.Violations.Select(item => new AlertViolationItemSnapshot
            {
                ProductGroup = item.ProductGroup,
                ProductGroupName = TrueApiProductGroupCatalog.TitleByCode(item.ProductGroup),
                Region = item.Region,
                ViolationResult = item.ViolationResult,
                ViolationResultName = item.ViolationResultName,
                ViolationNumber = item.ViolationNumber
            }).ToList()
        };
    }

    /// <summary>
    /// Календарная дата документа в локальной зоне сервера.
    /// </summary>
    private static string ToDateYmd(long unixValue)
    {
        var milliseconds = unixValue > 1_000_000_000_000 ? unixValue : unixValue * 1000;
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime.ToString("yyyy-MM-dd");
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

    private static AlertInstanceSnapshot ToInstanceSnapshot(InstanceEntity entity, DateTimeOffset now)
    {
        var hoursSinceUpdate = Math.Max(0, (now.LocalDateTime - entity.UpdatedAt).TotalHours);

        return new AlertInstanceSnapshot
        {
            Id = entity.Id,
            Name = entity.Name,
            GroupId = entity.GroupId,
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
