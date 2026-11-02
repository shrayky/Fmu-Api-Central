using Domain.Configuration.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VetisApi.Workers;

/// <summary>
/// Гасит входящие ВСД отдельно от загрузки: обмен с Меркурием не должен держать журнал.
/// </summary>
public sealed class VetisAutoQuenchWorker(
    IServiceScopeFactory scopeFactory,
    IVetisJobStatusStore jobs,
    ILogger<VetisAutoQuenchWorker> logger,
    TimeSpan? startDelay = null,
    TimeSpan? pace = null) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private VetisQuenchCursor _cursor = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Воркер автогашения входящих ВСД запущен");
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
                var now = DateTimeOffset.Now;
                if (!settings.Enable)
                {
                    delay = settings.AutoQuenchInterval();
                    Report(VetisJobPhase.Stopped, now, now.Add(delay), null, null);
                }
                else
                {
                    Report(VetisJobPhase.Running, now, null, null, null);
                    var quenched = await scope.ServiceProvider
                        .GetRequiredService<IVetisIncomingAutoQuencher>()
                        .QuenchBatchAsync(_cursor, stoppingToken);
                    // После полного прохода снова с начала: задержка могла выйти, загрузка — дописать номер.
                    _cursor = quenched.Finished ? new VetisQuenchCursor() : quenched.Cursor ?? new();
                    var finished = DateTimeOffset.Now;
                    if (quenched.Error.Length > 0)
                    {
                        delay = RetryDelay;
                        logger.LogError("Ошибка автогашения входящих ВСД: {Error}", quenched.Error);
                        Report(VetisJobPhase.Failed, finished, finished.Add(delay), quenched.QuenchedDocuments, quenched.Error);
                    }
                    else
                    {
                        delay = settings.AutoQuenchInterval();
                        Report(VetisJobPhase.Waiting, finished, finished.Add(delay), quenched.QuenchedDocuments, null);
                    }

                    if (pace is TimeSpan fixedPace)
                        delay = fixedPace;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ошибка воркера автогашения входящих ВСД");
                var failed = DateTimeOffset.Now;
                Report(VetisJobPhase.Failed, failed, failed.Add(RetryDelay), null, ex.Message);
            }

            await WaitAsync(delay, stoppingToken);
        }
    }

    private void Report(VetisJobPhase phase, DateTimeOffset at, DateTimeOffset? nextAt, int? count, string? error) =>
        jobs.Report(new VetisJobUpdate("autoQuench", "Автогашение", "погашено документов", 2, phase, at, nextAt, count, error));

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
