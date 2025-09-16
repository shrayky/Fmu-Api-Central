using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationMigrationService : IConfigurationMigrationService
    {
        private readonly ILogger<ConfigurationMigrationService> _logger;

        public ConfigurationMigrationService(ILogger<ConfigurationMigrationService> logger)
        {
            _logger = logger;
        }

        public async Task<Parameters> MigrateConfiguration(Parameters parameters)
        {
            if (!IsMigrationRequired(parameters))
                return parameters;

            _logger.LogInformation("Выполняется миграция конфигурации с версии {CurrentVersion} на {TargetVersion}",
                    parameters.Information.Version,
                    ApplicationInformation.Version);

            parameters.Information.Version = ApplicationInformation.Version;
            parameters.Information.Assembly = ApplicationInformation.Assembly;

            return parameters;
        }

        public bool IsMigrationRequired(Parameters parameters)
        {
            return parameters.Information.Version != ApplicationInformation.Version ||
                   parameters.Information.Assembly != ApplicationInformation.Assembly;
        }

        public async Task<Result<bool>> ValidateConfiguration(Parameters parameters)
        {
            if (parameters == null) 
                return Result.Failure<bool>("Конфигурация не может быть null");

            if (parameters.DatabaseConnection == null) 
                return Result.Failure<bool>("DatabaseConnection не может быть null");

            if (parameters.LoggerSettings == null)
                return Result.Failure<bool>("LoggerSettings не может быть null");

            if (parameters.ServerSettings == null) 
                return Result.Failure<bool>("ServerSettings не может быть null");

            return Result.Success(true);
        }
    }
}
