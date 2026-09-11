using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.CrptViolations.Services;

[AutoRegisterService]
public class CrptViolationsLoaderService(
    IOrganizationRepository organizationRepository,
    IApplicationState applicationState,
    ICrptStatisticsClient statisticsClient,
    ICrptViolationsRepository violationsRepository,
    ILogger<CrptViolationsLoaderService> logger) : ICrptViolationsLoaderService
{
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;
    private readonly IApplicationState _applicationState = applicationState;
    private readonly ICrptStatisticsClient _statisticsClient = statisticsClient;
    private readonly ICrptViolationsRepository _violationsRepository = violationsRepository;
    private readonly ILogger<CrptViolationsLoaderService> _logger = logger;

    /// <summary>
    /// Грузит отклонения и штрафы ЧЗ по организациям с включённой интеграцией и выбранной ЭЦП.
    /// </summary>
    public async Task<Result> Load(DateOnly today, CancellationToken cancellationToken, string? inn = null)
    {
        try
        {
            var organizations = await _organizationRepository.All();
            var requestedInn = (inn ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(requestedInn))
            {
                var organization = organizations.FirstOrDefault(item =>
                    string.Equals(item.Inn?.Trim(), requestedInn, StringComparison.OrdinalIgnoreCase));

                if (organization is null)
                    return Result.Failure($"Организация с ИНН {requestedInn} не найдена");

                return await LoadOrganization(organization, today, requested: true, cancellationToken);
            }

            foreach (var organization in organizations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await LoadOrganization(organization, today, requested: false, cancellationToken);
                if (result.IsFailure)
                    _logger.LogError("Ошибка загрузки отклонений ЧЗ для {Inn}: {Error}", organization.Inn, result.Error);
            }

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки отклонений ЧЗ");
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Грузит сегодняшний снимок организации; при ручном запросе возвращает причину пропуска.
    /// </summary>
    private async Task<Result> LoadOrganization(
        OrganizationEntity organization,
        DateOnly today,
        bool requested,
        CancellationToken cancellationToken)
    {
        if (!CrptViolationsLoadRules.CanLoad(organization))
            return requested
                ? Result.Failure("У организации не включена интеграция или не выбран сертификат ЭЦП")
                : Result.Success();

        var inn = organization.Inn.Trim();
        var token = _applicationState.TrueApiToken(inn);
        if (string.IsNullOrEmpty(token.Token))
            return requested
                ? Result.Failure($"Токен True API не получен для организации {organization.Name}")
                : Result.Success();

        var monthStart = CrptViolationsLoadRules.MonthStart(today);
        var existing = await _violationsRepository.GetDates(inn, monthStart, today);
        if (existing.IsFailure)
            return Result.Failure(existing.Error);

        var dayResult = await LoadToday(inn, token.Token, today, existing.Value, cancellationToken);
        if (dayResult.IsFailure)
        {
            _logger.LogError("Ошибка загрузки отклонений ЧЗ для {Inn} за {Date}: {Error}", inn, today, dayResult.Error);
            return requested ? dayResult : Result.Success();
        }

        return Result.Success();
    }

    /// <summary>
    /// Берёт месячный снимок ЧЗ и сохраняет сегодняшний день как снимок плюс прирост.
    /// </summary>
    private async Task<Result> LoadToday(
        string inn,
        string token,
        DateOnly today,
        IReadOnlyCollection<DateOnly> existingDates,
        CancellationToken cancellationToken)
    {
        var violations = await _statisticsClient.FetchViolations(inn, token, today, cancellationToken);
        if (violations.IsFailure)
            return Result.Failure(violations.Error);

        var penalty = await _statisticsClient.FetchPenalty(inn, token, today, cancellationToken);
        if (penalty.IsFailure)
            return Result.Failure(penalty.Error);

        var todayEntity = CrptViolationsResponseMapper.Map(inn, today, violations.Value, penalty.Value);
        var previousDate = CrptViolationsLoadRules.PreviousDate(existingDates, today);
        CrptViolationsDailyEntity? previous = null;

        if (previousDate is not null)
        {
            var previousResult = await _violationsRepository.Get(
                CrptViolationsLoadRules.DocumentId(inn, previousDate.Value));
            if (previousResult.IsFailure)
                return previousResult;
            previous = previousResult.Value;
        }

        CrptViolationsDelta.Apply(todayEntity, previous);
        return await _violationsRepository.Upsert(todayEntity);
    }
}
