using Domain.AppState.Interfaces;
using Domain.GisMt.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GisMtExchange.Workers;

/// <summary>
/// Фоновый обмен с GisMt каждые 30 секунд; флаг пуша сокращает ожидание.
/// </summary>
public class GisMtExchangeWorker(
    IServiceScopeFactory scopeFactory,
    IApplicationState applicationState,
    ILogger<GisMtExchangeWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IApplicationState _applicationState = applicationState;
    private readonly ILogger<GisMtExchangeWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Воркер обмена с Fmu-Api-GisMt запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var exchange = scope.ServiceProvider.GetRequiredService<IGisMtExchangeService>();
                await exchange.AutomaticExchange(stoppingToken);
                await WaitNextRound(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка воркера обмена с Fmu-Api-GisMt");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task WaitNextRound(CancellationToken stoppingToken)
    {
        var pendingAtStart = _applicationState.GisMtPushPending();

        for (var i = 0; i < 30; i++)
        {
            if (stoppingToken.IsCancellationRequested)
                return;

            if (!pendingAtStart && _applicationState.GisMtPushPending())
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
