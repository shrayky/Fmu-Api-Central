using System.Text;
using System.Text.Json;
using Domain.Attributes;
using Domain.Configuration.Options;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Http;

namespace CouchDb.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class IndexingService : IIndexingService
{
    private readonly ILogger<IndexingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexingService(ILogger<IndexingService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> EnsureIndexesExist(DatabaseConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Проверка наличия индексов для баз данных CouchDB.");

        var httpClientResult = _httpClientFactory.CreateClientSafely("CouchDbState", _logger);

        if (httpClientResult.IsFailure)
        {
            _logger.LogError("Не удалось создать HttpClient: {Error}", httpClientResult.Error);
            return false;
        }

        using var httpClient = httpClientResult.Value;
        httpClient.BaseAddress = new Uri(connection.NetAddress);

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{connection.UserName}:{connection.Password}"));
        httpClient.DefaultRequestHeaders.Authorization = new("Basic", authToken);

        var success = 0;

        var schema = DatabaseSchema.DatabaseIndexSchema();
        
        foreach (var index in schema)
        {
           if (await CreateIndexesForDatabase(httpClient, index.Key, index.Value, cancellationToken))
               success++;
        }

        if (success != schema.Count) 
            return false;
        
        _logger.LogInformation("Индексы для баз данных CouchDB созданы успешно");
        return true;

    }

    private async Task<bool> CreateIndexesForDatabase(HttpClient httpClient, string databaseName, object[] indexes,
        CancellationToken cancellationToken)
    {
        foreach (var index in indexes)
        {
            var indexJson = JsonSerializer.Serialize(index);
            var content = new StringContent(indexJson, Encoding.UTF8, "application/json");

            var responseResult = await httpClient.SendRequestSafelyAsync(
                client => client.PostAsync($"/{databaseName}/_index", content, cancellationToken),
                _logger,
                $"создание индекса для базы {databaseName}");

            if (responseResult.IsSuccess)
                _logger.LogDebug("Индекс для базы {DatabaseName} создан успешно", databaseName);
            else
                _logger.LogWarning("Не удалось создать индекс для базы {DatabaseName}: {StatusCode}", databaseName,
                    responseResult.Error);
        }

        return true;
    }

}