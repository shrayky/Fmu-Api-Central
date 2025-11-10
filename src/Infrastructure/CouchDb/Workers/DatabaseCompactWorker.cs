using Domain.AppState.Interfaces;
using Domain.Database.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CouchDb.Workers;

public class DatabaseCompactWorker : BackgroundService
{
    private readonly ILogger<DatabaseCompactWorker> _logger;
    private readonly IApplicationState _applicationState;
    private readonly IDataBaseMaintenanceService _maintenanceService;

    private const int StartDelay = 5;
    private const int WorkIntervalHours = 12; 
    
    public DatabaseCompactWorker(ILogger<DatabaseCompactWorker> logger, IApplicationState applicationState, IDataBaseMaintenanceService maintenanceService)
    {
        _logger = logger;
        _applicationState = applicationState;
        _maintenanceService = maintenanceService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(StartDelay), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_applicationState.DbState())
            {
                await Task.Delay(TimeSpan.FromMinutes(StartDelay), stoppingToken);
                continue;
            }

            _logger.LogWarning("Начато обслуживание базы данных");

            try
            {
                await _maintenanceService.CompactDatabase();
                _logger.LogWarning("Закончено обслуживание базы данных");
            }
            catch (Exception e)
            {
                _logger.LogError("Обслуживание БД завершено с ошибкой: {err}", e.Message);    
            }

            await Task.Delay(TimeSpan.FromHours(WorkIntervalHours), stoppingToken);
        }
    }
}