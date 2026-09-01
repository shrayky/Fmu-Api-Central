using Domain.AppState.Interfaces;
using Domain.Entitys.Organization.Interfaces;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TrueApiIntegration.Workers;

public class TrueApiTokenLoaderWorker : BackgroundService
{
    private readonly ILogger<TrueApiTokenLoaderWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITrueApiAuthService _authService;
    private readonly IApplicationState _applicationState;

    private DateTime _nextWorkDate = DateTime.Now;

    public TrueApiTokenLoaderWorker(
        ILogger<TrueApiTokenLoaderWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITrueApiAuthService authService,
        IApplicationState applicationState)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _authService = authService;
        _applicationState = applicationState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
#else
    await Task.Delay(TimeSpan.FromMinutes(TrueApiTokenDefaults.StartDelayMinutes), stoppingToken).ConfigureAwait(false);
#endif
        while (!stoppingToken.IsCancellationRequested)
        {
            if (DateTime.Now < _nextWorkDate)
            {
                await Task.Delay(TimeSpan.FromMinutes(TrueApiTokenDefaults.RefreshIntervalMinutes), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            await RefreshTokens(stoppingToken);
            _nextWorkDate = DateTime.Now.AddMinutes(TrueApiTokenDefaults.RefreshIntervalMinutes);
        }
    }

    private async Task RefreshTokens(CancellationToken stoppingToken)
    {
        try
        {
            if (!_applicationState.DbState())
                return;

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var organizations = await repository.All();

            foreach (var organisation in organizations)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                var inn = organisation.Inn;
                if (string.IsNullOrEmpty(inn))
                    continue;

                if (!(organisation.TrueApiIntegrationSettings?.Enable ?? false))
                    continue;

                var tokenData = _applicationState.TrueApiToken(inn);
                if (tokenData.Token != string.Empty)
                    continue;

                _logger.LogDebug("Начинаю получать токен true api для {inn}", inn);

                var trueApiSettings = organisation.TrueApiIntegrationSettings ?? new();
                var token = await _authService.GenerateToken(inn, trueApiSettings.Password, trueApiSettings.DigitalSignature);
                if (token.IsFailure)
                {
                    _logger.LogWarning("Не удалось получить токен true api для {inn}: {error}", inn, token.Error);
                    continue;
                }

                var tokenLifeUntil = DateTime.Now.AddHours(TrueApiTokenDefaults.LifeHours);
                _applicationState.UpdateTrueApiToken(inn, token.Value, tokenLifeUntil);
                _applicationState.MarkGisMtPushPending();

                _logger.LogInformation("Для {inn} получен новый токен, который действует до {tokenLifeUntil}", inn, tokenLifeUntil);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления токенов True API");
        }
    }
}
