using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.GisMt.Dto;
using Domain.GisMt.Enum;
using Domain.GisMt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.GisMt.Services;

[AutoRegisterService]
public class GisMtExchangeService(
    IParametersService parametersService,
    IGisMtClient gisMtClient,
    IOrganizationRepository organizationRepository,
    IApplicationState applicationState,
    ILogger<GisMtExchangeService> logger) : IGisMtExchangeService
{
    private readonly IParametersService _parametersService = parametersService;
    private readonly IGisMtClient _gisMtClient = gisMtClient;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;
    private readonly IApplicationState _applicationState = applicationState;
    private readonly ILogger<GisMtExchangeService> _logger = logger;

    /// <summary>
    /// Один круг обмена: пакет настроек на GisMt и запись статусов организаций.
    /// </summary>
    public async Task AutomaticExchange(CancellationToken cancellationToken)
    {
        var parameters = await _parametersService.Current();
        var settings = parameters.GisMtSettings;
        var serviceUrl = (settings.ServiceUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(serviceUrl))
            return;

        if (!settings.Enable)
            return;

        if (!settings.Enable && !_applicationState.GisMtPushPending())
            return;

        var packet = BuildPacket(parameters, cancellationToken);

        try
        {
            var putResult = await _gisMtClient.PutExchangeConfiguration(serviceUrl, packet, cancellationToken);

            if (putResult.IsSuccess)
                _applicationState.ClearGisMtPushPending();

            var statusResult = await _gisMtClient.FmuApiGisMtStatus(serviceUrl, cancellationToken);

            if (statusResult.IsFailure)
                return;

            await ApplyStatuses(statusResult.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обмена с Fmu-Api-GisMt");
        }
    }

    /// <summary>
    /// Ставит в очередь ручную операцию ГИС МТ для организации.
    /// </summary>
    public async Task<Result> ManualOperation(string organizationId, GisMtManualOperationKind kind, CancellationToken cancellationToken)
    {
        var organizationResult = await _organizationRepository.GetById(organizationId);
        
        if (organizationResult.IsFailure)
            return Result.Failure(organizationResult.Error);

        var organization = organizationResult.Value;
        var inn = (organization.Inn ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(inn))
            return Result.Failure("У организации не указан ИНН");

        var parameters = await _parametersService.Current();
        var settings = parameters.GisMtSettings;
        var serviceUrl = (settings.ServiceUrl ?? string.Empty).Trim();

        if (!settings.Enable)
            return Result.Failure("Обмен с Fmu-Api-GisMt выключен");

        if (string.IsNullOrWhiteSpace(serviceUrl))
            return Result.Failure("Не задан адрес сервиса Fmu-Api-GisMt");

        var trueApiToken = _applicationState.TrueApiToken(inn);

        if (string.IsNullOrEmpty(trueApiToken.Token))
            return Result.Failure($"Токен True API не получен для организации {organization.Name} не получен!");

        try
        {
            var request = new GisMtManualOperationRequest
            {
                Inn = inn,
                Token = trueApiToken.Token
            };

            if (kind == GisMtManualOperationKind.Documents)
            {
                var days = Math.Max(settings.DocumentsSyncDays, 1);
                request.DateTo = DateTime.Now;
                request.DateFrom = request.DateTo.Value.Date.AddDays(1 - days);
            }

            var result = await _gisMtClient.Operation(serviceUrl, kind, request, cancellationToken);

            if (result.IsFailure)
                return result;

            _applicationState.MarkGisMtPushPending();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка постановки операции Fmu-Api-GisMt для {OrganizationName}", organization.Name);
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Собирает пакет настроек и живых токенов для GisMt.
    /// </summary>
    private GisMtConfigurationPacket BuildPacket(Parameters parameters, CancellationToken cancellationToken)
    {
        var settings = parameters.GisMtSettings;
        var packet = new GisMtConfigurationPacket
        {
            Settings = new GisMtRemoteSettings
            {
                Enable = settings.Enable,
                MtDocumentsPollIntervalMinutes = settings.MtDocumentsPollIntervalMinutes,
                MarkRetentionDays = settings.MarkRetentionDays,
                DocumentsSyncDays = settings.DocumentsSyncDays,
                StockLoadEnabled = settings.StockLoadEnabled,
                StockLoadTime = settings.StockLoadTime
            },
            DatabaseConnection = parameters.DatabaseConnection,
            Tokens = []
        };

        foreach (var token in _applicationState.TrueApiTokens())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inn = (token.Inn ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(inn))
                continue;

            packet.Tokens.Add(new GisMtTokenItem
            {
                Inn = inn,
                Token = token.Token,
                Expired = token.LiveUntil
            });
        }

        return packet;
    }

    /// <summary>
    /// Пишет в организации изменившийся код и описание ошибки из ответа GisMt.
    /// </summary>
    private async Task ApplyStatuses(GisMtStatusResponse status, CancellationToken cancellationToken)
    {
        if (!_applicationState.DbState())
            return;

        var organizations = await _organizationRepository.All();

        var byInn = organizations
            .Where(item => !string.IsNullOrWhiteSpace(item.Inn))
            .GroupBy(item => item.Inn.Trim())
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var item in status.Organizations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inn = (item.Inn ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(inn))
                continue;

            if (!byInn.TryGetValue(inn, out var entity))
                continue;

            var description = ErrorDescription(item.StatusCode, item.Description);
            entity.NormalizeGisMtLastStatus();
            var last = entity.GisMtLastStatus ??= new();

            if (last.Code == item.StatusCode
                && (last.Description ?? string.Empty) == description)
                continue;

            last.Code = item.StatusCode;
            last.Description = description;
            last.At = item.At;

            var update = await _organizationRepository.Update(entity);
            if (update.IsFailure)
                _logger.LogWarning("Не удалось сохранить статус ГИС МТ для {Inn}: {Error}", inn, update.Error);
        }
    }

    /// <summary>
    /// Возвращает описание только для неуспешного кода ответа.
    /// </summary>
    private static string ErrorDescription(int statusCode, string? description)
    {
        if (statusCode >= 200 && statusCode < 300)
            return string.Empty;

        return description ?? string.Empty;
    }
}
