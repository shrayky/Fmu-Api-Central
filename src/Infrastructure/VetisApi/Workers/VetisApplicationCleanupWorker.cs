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
    IVetisJobStatusStore jobs,
    ILogger<VetisApplicationCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер очистки заявок Ветис запущен");
        Report(VetisJobPhase.Starting, DateTimeOffset.Now, DateTimeOffset.Now.Add(StartDelay), null, null);
        await WaitAsync(StartDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                Report(VetisJobPhase.Running, DateTimeOffset.Now, null, null, null);
                var deleted = await scope.ServiceProvider.GetRequiredService<IVetisApplicationCleaner>().CleanAsync(stoppingToken);
                var finished = DateTimeOffset.Now;
                Report(VetisJobPhase.Waiting, finished, finished.Add(Interval), deleted, null);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера очистки заявок Ветис");
                var failed = DateTimeOffset.Now;
                Report(VetisJobPhase.Failed, failed, failed.Add(Interval), null, ex.Message);
            }

            await WaitAsync(Interval, stoppingToken);
        }
    }

    private void Report(VetisJobPhase phase, DateTimeOffset at, DateTimeOffset? nextAt, int? count, string? error) =>
        jobs.Report(new VetisJobUpdate("applicationCleanup", "Очистка заявок", "удалено заявок", 3, phase, at, nextAt, count, error));

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
