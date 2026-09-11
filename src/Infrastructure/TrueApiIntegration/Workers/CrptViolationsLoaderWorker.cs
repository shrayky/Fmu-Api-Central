using Domain.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TrueApiIntegration.Workers;

/// <summary>
/// Фоновая загрузка отклонений и штрафов ЧЗ раз в час.
/// </summary>
public class CrptViolationsLoaderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CrptViolationsLoaderWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<CrptViolationsLoaderWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Воркер загрузки отклонений ЧЗ запущен");

#if DEBUG
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
#else
        await Task.Delay(TimeSpan.FromMinutes(CrptViolationsDefaults.StartDelayMinutes), stoppingToken);
#endif

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var loader = scope.ServiceProvider.GetRequiredService<ICrptViolationsLoaderService>();
                await loader.Load(DateOnly.FromDateTime(DateTime.Today), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка воркера загрузки отклонений ЧЗ");
            }

            await Task.Delay(TimeSpan.FromMinutes(CrptViolationsDefaults.PollIntervalMinutes), stoppingToken);
        }
    }
}
