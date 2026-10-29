using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VetisApi.Workers;

/// <summary>
/// Сроки хранения заявок задаются в днях, поэтому чистка раз в час достаточно точна.
/// </summary>
public sealed class VetisApplicationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<VetisApplicationCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер очистки заявок Ветис запущен");
        await WaitAsync(StartDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IVetisApplicationCleaner>().CleanAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера очистки заявок Ветис");
            }

            await WaitAsync(Interval, stoppingToken);
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
