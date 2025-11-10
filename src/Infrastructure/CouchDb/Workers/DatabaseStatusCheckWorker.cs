using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Database.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CouchDb.Workers
{
    public class DatabaseStatusCheckWorker : BackgroundService
    {
        private readonly ILogger<DatabaseStatusCheckWorker> _logger;
        private readonly IParametersService _parametersService;
        private readonly IApplicationState _applicationState;
        private readonly IDbStatusService _databaseStatusService;
        private readonly IIndexingService _indexingService;

        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

        public DatabaseStatusCheckWorker(ILogger<DatabaseStatusCheckWorker> logger, IParametersService parameters, IApplicationState applicationState, IDbStatusService databaseStatusService, IIndexingService indexingService)
        {
            _logger = logger;
            _parametersService = parameters;
            _applicationState = applicationState;
            _databaseStatusService = databaseStatusService;
            _indexingService = indexingService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var needToEnsureDatabaseExist = true;
            var needToEnsureDefaultUser = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_checkInterval, stoppingToken);

                var appConfig = await _parametersService.Current();
                var databaseConfig = appConfig.DatabaseConnection;

                await CheckCouchOnlineState(databaseConfig, stoppingToken);

                if (needToEnsureDatabaseExist)
                    needToEnsureDatabaseExist = !await EnsureDatabasesExists(databaseConfig, stoppingToken);

                if (needToEnsureDefaultUser)
                    needToEnsureDefaultUser = !await EnsureDefaultUserExists(stoppingToken);
            }
        }

        private async Task CheckCouchOnlineState(DatabaseConnection databaseConfig, CancellationToken stoppingToken)
        {
            var dbOnline = _applicationState.DbState();

            if (!databaseConfig.Enable && dbOnline)
            {
                _logger.LogCritical("Изменение статуса доступности базы данных, новый статус - отключена");
                _applicationState.DbStateUpdate(false);
                return;
            }

            if (!databaseConfig.Enable)
                return;

            var nowState = await _databaseStatusService.CheckAvailability(databaseConfig.NetAddress, stoppingToken);

            if (nowState == dbOnline)
                return;

            _logger.LogCritical("Изменение статуса доступности базы данных {beforeCheck} -> {aftetCheck}", dbOnline, nowState);
            _applicationState.DbStateUpdate(nowState);
        }

        private async Task<bool> EnsureDatabasesExists(DatabaseConnection databaseConfig, CancellationToken stoppingToken)
        {
            var dbOnline = _applicationState.DbState();

            if (!dbOnline)
                return false;

            var result = await _databaseStatusService.EnsureDatabasesExists(databaseConfig, DatabaseSchema.All(), stoppingToken);
            
            if (!result)
                return false;
            
            var indexingResult = await _indexingService.EnsureIndexesExist(databaseConfig, stoppingToken);
            
            return indexingResult;
        }

        private async Task<bool> EnsureDefaultUserExists(CancellationToken cancellationToken)
        {
            var dbOnline = _applicationState.DbState();

            if (!dbOnline)
                return false;

            return await _databaseStatusService.EnsureDefaultUserExists(cancellationToken);
        }
    }
}
