using Domain.Configuration.Interfaces;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EtrnSaby.Workers;

/// <summary>
/// Интервал читается из настроек на каждом круге, поэтому правка применяется без перезапуска.
/// </summary>
public sealed class EtrnIncomingWorker(
    IServiceScopeFactory scopeFactory,
    IEtrnJobStatusStore jobs,
    ILogger<EtrnIncomingWorker> logger,
    TimeSpan? startDelay = null) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер загрузки входящих ЭТрН запущен");
        var opening = startDelay ?? StartDelay;
        Report(EtrnJobPhase.Starting, DateTimeOffset.Now, DateTimeOffset.Now.Add(opening), null, null);
        await WaitAsync(opening, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = RetryDelay;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var settings = (await scope.ServiceProvider.GetRequiredService<IParametersService>().Current())
                    .EtrnSettings ?? new EtrnSettings();
                Report(EtrnJobPhase.Running, DateTimeOffset.Now, null, null, null);
                var loader = scope.ServiceProvider.GetRequiredService<IEtrnIncomingLoader>();
                var saved = await loader.LoadAsync(stoppingToken);
                // Только после успешной загрузки: сбой оставляет короткий повтор, а не интервал.
                delay = settings.IncomingInterval();
                var finished = DateTimeOffset.Now;
                Report(EtrnJobPhase.Waiting, finished, finished.Add(delay), saved, loader.LastError);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера загрузки входящих ЭТрН");
                var failed = DateTimeOffset.Now;
                Report(EtrnJobPhase.Failed, failed, failed.Add(RetryDelay), null, ex.Message);
            }

            await WaitAsync(delay, stoppingToken);
        }
    }

    private void Report(EtrnJobPhase phase, DateTimeOffset at, DateTimeOffset? nextAt, int? count, string? error) =>
        jobs.Report(new EtrnJobUpdate(
            "etrnIncoming",
            "Загрузка входящих ЭТрН",
            "записано накладных",
            1,
            phase,
            at,
            nextAt,
            count,
            error));

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

/// <summary>
/// Сроки хранения запросов задаются в сутках, поэтому чистка раз в час достаточно точна.
/// </summary>
public sealed class EtrnRequestCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IEtrnJobStatusStore jobs,
    ILogger<EtrnRequestCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер очистки запросов ЭТрН запущен");
        Report(EtrnJobPhase.Starting, DateTimeOffset.Now, DateTimeOffset.Now.Add(StartDelay), null, null);
        await WaitAsync(StartDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                Report(EtrnJobPhase.Running, DateTimeOffset.Now, null, null, null);
                var deleted = await scope.ServiceProvider
                    .GetRequiredService<IEtrnRequestCleaner>()
                    .CleanAsync(stoppingToken);
                var finished = DateTimeOffset.Now;
                Report(EtrnJobPhase.Waiting, finished, finished.Add(Interval), deleted, null);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера очистки запросов ЭТрН");
                var failed = DateTimeOffset.Now;
                Report(EtrnJobPhase.Failed, failed, failed.Add(Interval), null, ex.Message);
            }

            await WaitAsync(Interval, stoppingToken);
        }
    }

    private void Report(EtrnJobPhase phase, DateTimeOffset at, DateTimeOffset? nextAt, int? count, string? error) =>
        jobs.Report(new EtrnJobUpdate(
            "etrnCleanup",
            "Очистка запросов",
            "удалено запросов",
            2,
            phase,
            at,
            nextAt,
            count,
            error));

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
