using System.Diagnostics;
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
    IVetisJobStatusStore jobs,
    ILogger<VetisIncomingWorker> logger,
    TimeSpan? startDelay = null) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    // Одна страница на круг при минутной паузе держала бы журнал без обновления часами.
    private static readonly TimeSpan ScanBudget = TimeSpan.FromSeconds(10);

    private string _sortAfter = "";

    private bool _sortDone;

    private string _transportAfter = "";

    private bool _transportDone;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер загрузки входящих ВСД запущен");
        var opening = startDelay ?? StartDelay;
        Report(VetisJobPhase.Starting, DateTimeOffset.Now, DateTimeOffset.Now.Add(opening), null, null);
        await WaitAsync(opening, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = RetryDelay;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var settings = (await scope.ServiceProvider.GetRequiredService<IParametersService>().Current()).VetisSettings ?? new();
                if (!_sortDone || !_transportDone)
                    Report(VetisJobPhase.Preparing, DateTimeOffset.Now, null, null, null);

                if (!_sortDone)
                {
                    var budget = Stopwatch.StartNew();
                    while (!_sortDone && budget.Elapsed < ScanBudget)
                    {
                        var aligned = await scope.ServiceProvider
                            .GetRequiredService<IVetisIncomingSortAligner>()
                            .AlignBatchAsync(_sortAfter, stoppingToken);
                        if (aligned.IsFailure)
                            throw new InvalidOperationException(aligned.Error);

                        _sortAfter = aligned.Value.AfterId;
                        _sortDone = aligned.Value.Finished;
                    }
                }

                if (!_transportDone)
                {
                    var sweepBudget = Stopwatch.StartNew();
                    while (!_transportDone && sweepBudget.Elapsed < ScanBudget)
                    {
                        var swept = await scope.ServiceProvider
                            .GetRequiredService<IVetisIncomingTransportSweep>()
                            .SweepBatchAsync(_transportAfter, stoppingToken);
                        if (swept.IsFailure)
                            throw new InvalidOperationException(swept.Error);

                        _transportAfter = swept.Value.AfterId;
                        _transportDone = swept.Value.Finished;
                    }
                }

                // Пока снятие не закончено, загрузка дописала бы ВСД между Find и Save снятия, и сохранение вернуло бы старый список.
                if (_sortDone && _transportDone)
                {
                    var now = DateTimeOffset.Now;
                    if (settings.Enable)
                    {
                        Report(VetisJobPhase.Running, now, null, null, null);
                        var saved = await scope.ServiceProvider.GetRequiredService<IVetisIncomingLoader>().LoadAsync(stoppingToken);
                        // Только после успешной загрузки: сбой оставляет короткий повтор, а не интервал журнала.
                        delay = settings.IncomingInterval();
                        var finished = DateTimeOffset.Now;
                        Report(VetisJobPhase.Waiting, finished, finished.Add(delay), saved, null);
                    }
                    else
                    {
                        delay = settings.IncomingInterval();
                        Report(VetisJobPhase.Stopped, now, now.Add(delay), null, null);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера загрузки входящих ВСД");
                var failed = DateTimeOffset.Now;
                Report(VetisJobPhase.Failed, failed, failed.Add(RetryDelay), null, ex.Message);
            }

            await WaitAsync(delay, stoppingToken);
        }
    }

    private void Report(VetisJobPhase phase, DateTimeOffset at, DateTimeOffset? nextAt, int? count, string? error) =>
        jobs.Report(new VetisJobUpdate("incoming", "Получение ВСД", "записано документов", 1, phase, at, nextAt, count, error));

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
