using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CouchDb.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class CouchDbHealthService : IDbHealthService
    {
        private readonly Context _dbContext;
        private readonly IParametersService _parametersService;
        private readonly ILogger<CouchDbHealthService> _logger;

        public CouchDbHealthService(Context dbContext, IParametersService parametersService, ILogger<CouchDbHealthService> logger)
        {
            _dbContext = dbContext;
            _parametersService = parametersService;
            _logger = logger;
        }

        public async Task<bool> IsDatabaseEnabled()
        {
            var configuration = await _parametersService.Current();
            return configuration.DatabaseConnection.Enable;
        }

        public async Task<bool> IsDatabaseAccessible(string databaseName)
        {
            try
            {
                var info = databaseName switch
                {
                    DatabaseSchema.Users => await _dbContext.Users.GetInfoAsync(),
                    DatabaseSchema.Instance => await _dbContext.FmuApiInstances.GetInfoAsync(),
                    DatabaseSchema.SoftwareUpdateFiles => await _dbContext.SoftwareUpdateFiles.GetInfoAsync(),
                    _ => throw new ArgumentException($"Неизвестная база данных: {databaseName}")
                };

                return info != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "База данных {DatabaseName} недоступна", databaseName);
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> GetAllDatabasesStatus()
        {
            var databases = DatabaseSchema.All();
            var status = new Dictionary<string, bool>();

            foreach (var dbName in databases)
            {
                status[dbName] = await IsDatabaseAccessible(dbName);
            }

            return status;
        }

        public async Task<bool> IsConnectionHealthy()
        {
            var isOnline = await IsDatabaseEnabled();

            if (!isOnline)
                return false;

            return await IsDatabaseAccessible(DatabaseSchema.Users);
        }
    }
}
