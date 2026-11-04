using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Organization.Services;

[AutoRegisterService]
public class XApiKeyLoadService : IXApiKeyLoadService
{
    private readonly ITrueApiAuthService _authService;
    private readonly IApplicationState _applicationState;
    private readonly IOrganizationRepository _repository;
    private readonly ILogger<XApiKeyLoadService> _logger;

    public XApiKeyLoadService(
        ITrueApiAuthService authService,
        IApplicationState applicationState,
        IOrganizationRepository repository,
        ILogger<XApiKeyLoadService> logger)
    {
        _authService = authService;
        _applicationState = applicationState;
        _repository = repository;
        _logger = logger;
    }

    public async Task Refresh(IEnumerable<OrganizationEntity> organizations, CancellationToken cancellationToken)
    {
        foreach (var organization in organizations)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var inn = (organization.Inn ?? string.Empty).Trim();
            if (inn.Length == 0)
                continue;

            var settings = organization.TrueApiIntegrationSettings ?? new();
            if (string.IsNullOrWhiteSpace(settings.DigitalSignature))
                continue;

            var cached = _applicationState.XApiKey(inn);
            if (!string.IsNullOrEmpty(cached.Token))
                continue;

            var postponedUntil = _applicationState.XApiKeyPostponedUntil(inn);
            if (postponedUntil != null && postponedUntil.Value > DateTime.Now)
                continue;

            var generated = await _authService.GenerateXApiKey(inn, settings.Password, settings.DigitalSignature);
            if (generated.IsFailure)
            {
                _logger.LogWarning("Не удалось получить x-api-key для {inn}: {error}", inn, generated.Error);
                var next = XApiKeyRetry.NextAttempt(generated.Error, DateTime.Now);
                if (next != null)
                {
                    _applicationState.PostponeXApiKey(inn, next.Value);
                    await MarkXApiKeyExpired(organization, inn);
                }
                continue;
            }

            var session = generated.Value;
            _applicationState.ClearXApiKeyPostpone(inn);
            _applicationState.UpdateXApiKey(inn, session.Token, session.LiveUntil);

            var fresh = await _repository.GetById(organization.Id);
            if (fresh.IsFailure)
            {
                _logger.LogWarning("Не удалось перечитать организацию {id} для срока x-api-key: {error}", organization.Id, fresh.Error);
                continue;
            }

            fresh.Value.XApiKeyExpiresAt = session.LiveUntil;
            var saved = await _repository.Update(fresh.Value);
            if (saved.IsFailure)
                _logger.LogWarning("Не удалось сохранить срок x-api-key для {inn}: {error}", inn, saved.Error);
        }
    }

    private async Task MarkXApiKeyExpired(OrganizationEntity organization, string inn)
    {
        var fresh = await _repository.GetById(organization.Id);
        if (fresh.IsFailure)
        {
            _logger.LogWarning("Не удалось перечитать организацию {id} для срока x-api-key: {error}", organization.Id, fresh.Error);
            return;
        }

        if (fresh.Value.XApiKeyExpiresAt is DateTime expiresAt && expiresAt < DateTime.Now)
            return;

        // Обмен считает срок живым, пока ExpiresAt >= now.
        fresh.Value.XApiKeyExpiresAt = DateTime.Now.AddSeconds(-1);
        var saved = await _repository.Update(fresh.Value);
        if (saved.IsFailure)
            _logger.LogWarning("Не удалось сохранить срок x-api-key для {inn}: {error}", inn, saved.Error);
    }
}
