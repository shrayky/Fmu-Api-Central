using CouchDb.Repositories;
using Domain.Attributes;
using Domain.Configuration.Options;
using Domain.Database.Interfaces;
using Domain.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Http;
using System.Net;
using Domain.Entitys;

namespace CouchDb.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class CouchDbStatusService : IDbStatusService
    {
        private readonly ILogger<CouchDbStatusService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        public CouchDbStatusService(IHttpClientFactory httpClientFactory, ILogger<CouchDbStatusService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> CheckAvailability(string databaseUrl, CancellationToken cancellationToken = default)
        {
            var httpClientResult = _httpClientFactory.CreateClientSafely("CouchDbState", _logger);
            if (httpClientResult.IsFailure)
            {
                _logger.LogError("Не удалось создать HttpClient: {Error}", httpClientResult.Error);
                return false;
            }

            using var httpClient = httpClientResult.Value;
            httpClient.BaseAddress = new Uri(databaseUrl);

            var responseResult = await httpClient.SendRequestSafelyAsync(
                client => client.GetAsync("", cancellationToken),
                _logger,
                "проверка состояния CouchDB");

            if (responseResult.IsFailure)
            {
                _logger.LogError("Ошибка при проверке состояния CouchDB: {Error}", responseResult.Error);
                return false;
            }

            return responseResult.Value.IsSuccessStatusCode;
        }

        public async Task<bool> EnsureDatabasesExists(DatabaseConnection connection, string[] databasesNames, CancellationToken cancellationToken)
        {
            var httpClientResult = _httpClientFactory.CreateClientSafely("CouchDbState", _logger);
            if (httpClientResult.IsFailure)
            {
                _logger.LogError("Не удалось создать HttpClient: {Error}", httpClientResult.Error);
                return false;
            }

            using var httpClient = httpClientResult.Value;
            httpClient.BaseAddress = new Uri(connection.NetAddress);
            
            var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{connection.UserName}:{connection.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = new("Basic", authToken);

            foreach (var dbName in databasesNames)
            {
                var responseResult = await httpClient.SendRequestSafelyAsync(client => client.GetAsync($"/{dbName}", cancellationToken),
                                                                                _logger,
                                                                                $"проверка существования {dbName}");

                if (responseResult.IsFailure)
                    return false;

                if (responseResult.Value.IsSuccessStatusCode)
                    continue;

                if (responseResult.Value.StatusCode == HttpStatusCode.NotFound)
                {
                    var creationResult = await httpClient.SendRequestSafelyAsync(client => client.PutAsync($"/{dbName}", null, cancellationToken),
                                                                                    _logger,
                                                                                    $"создание (put) {dbName}");

                    if (creationResult.IsSuccess)
                    {
                        _logger.LogInformation("База данных {DatabaseName} успешно создана", dbName);
                    }
                    else
                    {
                        _logger.LogError("Не удалось создать базу данных {DatabaseName}: {StatusCode}", dbName, creationResult.Value.StatusCode);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> EnsureDefaultUserExists(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var usersRepository = scope.ServiceProvider.GetRequiredService<UsersRepository>();

            if (await usersRepository.RecordCount() > 0)
                return true;

            UserEntity userEntity = new()
            {
                Name = "admin",
                Password = "admin",
            };

            try
            {
                await usersRepository.CreateAsync(userEntity);
                _logger.LogCritical("В базе данных создан пользователь по умолчанию");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка создания пользователя по умолчанию");
                return false;
            }

            return true;
        }
    }
}
