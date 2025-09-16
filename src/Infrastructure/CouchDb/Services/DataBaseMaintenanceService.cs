using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CouchDb.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class DataBaseMaintenanceService : IDataBaseMaintenanceService
{
     private readonly ILogger<DataBaseMaintenanceService> _logger;
     private readonly IApplicationState _appState;
     private readonly Context _dbContext;

     public DataBaseMaintenanceService(ILogger<DataBaseMaintenanceService> logger, IApplicationState appState, Context dbContext)
     {
          _logger = logger;
          _appState = appState;
          _dbContext = dbContext;
     }

     public async Task<bool> CompactDatabase()
     {
          if (!_appState.DbState())
               return false;

          try
          {
               await _dbContext.Users.CompactAsync();
               await _dbContext.FmuApiInstances.CompactAsync();
               await _dbContext.SoftwareUpdateFiles.CompactAsync();
          }
          catch (Exception e)
          {
               _logger.LogError("Ошибка сжатия БД: {err}", e.Message);
               return false;
          }

          return true;
     }
}