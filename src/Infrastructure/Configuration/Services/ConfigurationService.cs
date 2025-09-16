using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationService : IParametersService
    {
        private readonly Lazy<IConfigurationFileManager> _fileManager;
        private readonly Lazy<IConfigurationSerializer> _serializer;
        private readonly Lazy<IConfigurationCacheManager> _cacheManager;
        private readonly Lazy<IConfigurationMigrationService> _migrationService;
        private readonly ILogger<ConfigurationService> _logger;

        public ConfigurationService(IServiceProvider service, ILogger<ConfigurationService> logger) 
        {
            _fileManager = new Lazy<IConfigurationFileManager>(() => service.GetRequiredService<IConfigurationFileManager>());
            _serializer = new Lazy<IConfigurationSerializer>(() => service.GetRequiredService<IConfigurationSerializer>());
            _cacheManager = new Lazy<IConfigurationCacheManager>(() => service.GetRequiredService<IConfigurationCacheManager>());
            _migrationService = new Lazy<IConfigurationMigrationService>(() => service.GetRequiredService<IConfigurationMigrationService>());

            _logger = logger;
        }

        public async Task<Parameters> Current()
        {
            var cachedResult = await _cacheManager.Value.GetCachedConfiguration();
            
            if (cachedResult.IsSuccess)
                return cachedResult.Value;

            var configResult = await LoadConfiguration();
            if (configResult.IsSuccess)
            {
                _cacheManager.Value.CacheConfiguration(configResult.Value);
                return configResult.Value;
            }

            _logger.LogError("Не удалось загрузить конфигурацию: {Error}", configResult.Error);
            return new Parameters();
        }

        public async Task<bool> Update(Parameters parameters)
        {
            var validationResult = await _migrationService.Value.ValidateConfiguration(parameters);

            if (validationResult.IsFailure)
            {
                _logger.LogError("Некорректная конфигурация: {Error}", validationResult.Error);
                return false;
            }

            var migratedConfig = await _migrationService.Value.MigrateConfiguration(parameters);

            await _fileManager.Value.SaveConfiguration(migratedConfig);
            await _fileManager.Value.CreateBackup(migratedConfig);
            _cacheManager.Value.CacheConfiguration(migratedConfig);

            _logger.LogInformation("Конфигурация обновлена");
            return true;
        }

        private async Task<Result<Parameters>> LoadConfiguration()
        {
            var configResult = await _fileManager.Value.LoadConfiguration();

            if (configResult.IsFailure)
                return configResult;

            return Result.Success(await _migrationService.Value.MigrateConfiguration(configResult.Value));
        }
    }
}
