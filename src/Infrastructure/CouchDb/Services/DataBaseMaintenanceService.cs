using CouchDb.DatabaseScheme;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Http;
using System.Text;

namespace CouchDb.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class DataBaseMaintenanceService(
    ILogger<DataBaseMaintenanceService> logger,
    IApplicationState appState,
    IParametersService parametersService,
    IHttpClientFactory httpClientFactory) : IDataBaseMaintenanceService
{
    private readonly ILogger<DataBaseMaintenanceService> _logger = logger;
    private readonly IApplicationState _appState = appState;
    private readonly IParametersService _parametersService = parametersService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<bool> CompactDatabase()
    {
        if (!_appState.DbState())
            return false;

        var connection = (await _parametersService.Current()).DatabaseConnection;
        var httpClientResult = _httpClientFactory.CreateClientSafely("CouchDbCompact", _logger);

        if (httpClientResult.IsFailure)
        {
            _logger.LogError("Ошибка сжатия БД: {err}", httpClientResult.Error);
            return false;
        }

        using var httpClient = httpClientResult.Value;
        httpClient.BaseAddress = new Uri(connection.NetAddress);

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{connection.UserName}:{connection.Password}"));
        httpClient.DefaultRequestHeaders.Authorization = new("Basic", authToken);

        var allSucceeded = true;

        foreach (var databaseName in DatabaseNames.All())
        {
            if (!await CompactSingleDatabase(httpClient, databaseName))
                allSucceeded = false;
        }

        return allSucceeded;
    }

    /// <summary>
    /// POST /{db}/_compact по всем именам, не только по четырём коллекциям старого Context.
    /// </summary>
    private async Task<bool> CompactSingleDatabase(HttpClient httpClient, string databaseName)
    {
        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync($"/{databaseName}/_compact", content);

            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Ошибка сжатия БД {DatabaseName}: {StatusCode} {Body}", databaseName, response.StatusCode, body);
            return false;
        }
        catch (Exception e)
        {
            _logger.LogError("Ошибка сжатия БД {DatabaseName}: {err}", databaseName, e.Message);
            return false;
        }
    }
}
