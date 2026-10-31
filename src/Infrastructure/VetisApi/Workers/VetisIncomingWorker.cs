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
    ILogger<VetisIncomingWorker> logger) : BackgroundService
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
        await WaitAsync(StartDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = RetryDelay;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var settings = (await scope.ServiceProvider.GetRequiredService<IParametersService>().Current()).VetisSettings;
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
                    if (settings.Enable)
                        await scope.ServiceProvider.GetRequiredService<IVetisIncomingLoader>().LoadAsync(stoppingToken);

                    // Только после успешной загрузки: сбой оставляет короткий повтор, а не интервал журнала.
                    delay = settings.IncomingInterval();
                }
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
