using CouchDb.DatabaseScheme;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Http;

namespace CouchDb.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class DataBaseMaintenanceService(
    ILogger<DataBaseMaintenanceService> logger,
    IApplicationState appState,
    Context dbContext,
    IHttpClientFactory httpClientFactory) : IDataBaseMaintenanceService
{
     private readonly ILogger<DataBaseMaintenanceService> _logger = logger;
     private readonly IApplicationState _appState = appState;
     private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
     private readonly Context _dbContext = dbContext;

    public async Task<bool> CompactDatabase()
    {
        if (!_appState.DbState())
            return false;

        try
        {
            await _dbContext.Users.CompactAsync();
            await _dbContext.FmuApiInstances.CompactAsync();
            await _dbContext.SoftwareUpdateFiles.CompactAsync();
            await _dbContext.MarkCheckStatistics.CompactAsync();
        }
        catch (Exception e)
        {
            _logger.LogError("Ошибка сжатия БД: {err}", e.Message);
            return false;
        }

        return true;
    }

    public async Task<Result> CompactDatabases()
    {
        if (!_appState.DbState())
            return Result.Failure("База данных недоступна");

        var httpClientResult = _httpClientFactory.CreateClientSafely("CouchDbCompact", _logger);

        if (httpClientResult.IsFailure)
        {
            var err = $"Не удалось создать HttpClient: {httpClientResult.Error}";
            return Result.Failure(err);
        }

        foreach (var dbName in DatabaseNames.All())
        {

        }

        return Result.Success();
    }
}