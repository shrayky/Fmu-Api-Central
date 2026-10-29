using Domain.Configuration.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VetisApi.Workers;

/// <summary>
/// Загружает входящие ВСД. Интервал читается из настроек на каждом круге, поэтому правка применяется без перезапуска.
/// </summary>
public sealed class VetisIncomingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<VetisIncomingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер загрузки входящих ВСД запущен");
        await WaitAsync(StartDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = RetryDelay;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var settings = (await scope.ServiceProvider.GetRequiredService<IParametersService>().Current()).VetisSettings;
                delay = settings.IncomingInterval();
                if (settings.Enable)
                    await scope.ServiceProvider.GetRequiredService<IVetisIncomingLoader>().LoadAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера загрузки входящих ВСД");
            }

            await WaitAsync(delay, stoppingToken);
        }
    }

    private static async Task WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
