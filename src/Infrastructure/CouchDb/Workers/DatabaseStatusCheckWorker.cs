using CouchDb.DatabaseScheme;
using CouchDb.Interfaces;
using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Database.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CouchDb.Workers
{
    public class DatabaseStatusCheckWorker(
        ILogger<DatabaseStatusCheckWorker> logger,
        IParametersService parameters,
        IApplicationState applicationState,
        IDbStatusService databaseStatusService,
        IIndexingService indexingService) : BackgroundService
    {
        private readonly ILogger<DatabaseStatusCheckWorker> _logger = logger;
        private readonly IParametersService _parametersService = parameters;
        private readonly IApplicationState _applicationState = applicationState;
        private readonly IDbStatusService _databaseStatusService = databaseStatusService;
        private readonly IIndexingService _indexingService = indexingService;

        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var needToEnsureDatabaseExist = true;
            var needToEnsureDatabaseIndex = true;

            var needToEnsureDefaultUser = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_checkInterval, stoppingToken);

                var appConfig = await _parametersService.Current();
                var databaseConfig = appConfig.DatabaseConnection;

                await CheckCouchOnlineState(databaseConfig, stoppingToken);

                if (_applicationState.DbState())
                {
                    if (needToEnsureDatabaseExist)
                        needToEnsureDatabaseExist = !await EnsureDatabasesExists(databaseConfig, stoppingToken);

                    if (needToEnsureDatabaseIndex)
                        needToEnsureDatabaseIndex = !await EnsureDatabaseIndexes(databaseConfig, stoppingToken);

                    if (needToEnsureDefaultUser)
                        needToEnsureDefaultUser = !await EnsureDefaultUserExists(stoppingToken);
                }
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
            var result = await _databaseStatusService.EnsureDatabasesExists(databaseConfig, DatabaseNames.All(), stoppingToken);
            
            if (!result)
                return false;

            return true;
        }

        private async Task<bool> EnsureDatabaseIndexes(DatabaseConnection databaseConfig, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Проверка наличия индексов для баз данных CouchDB.");

            var indexEnsureResult = await _indexingService.EnsureIndexesExist(databaseConfig, stoppingToken);

            if (indexEnsureResult.IsSuccess)
            {
                _logger.LogInformation("Индексы для баз данных CouchDB созданы успешно");
            }
            else
            {
                _logger.LogError("Ошибка проверки индексов базы данных CouchDb: {err}", indexEnsureResult.Error);

                return false;
            }

            return true;
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