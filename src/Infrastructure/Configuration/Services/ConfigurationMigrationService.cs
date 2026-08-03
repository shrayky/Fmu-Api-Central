using Configuration.Migrations;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Services;

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

        _logger.LogInformation("Выполняется миграция конфигурации с версии {CurrentVersion}.{CurrentAssembly} на {TargetVersion}.{TargetAssembly}",
                parameters.Information.Version,
                parameters.Information.Assembly,
                ApplicationInformation.Version,
                ApplicationInformation.Assembly);

        // Миграции для перехода с версий ниже 1.6
        if (IsBelowVersion(parameters, version: 1, assembly: 6))
        {
            if (BotAlertSettingsToNestedMigration.Apply(parameters.BotSettings))
            {
                _logger.LogInformation(
                    "Миграция настроек оповещений: плоские поля ЛМ/ТС ПИоТ перенесены во вложенные объекты");
            }
        }

        parameters.Information.Version = ApplicationInformation.Version;
        parameters.Information.Assembly = ApplicationInformation.Assembly;

        return parameters;
    }

    public bool IsMigrationRequired(Parameters parameters)
    {
        return parameters.Information.Version != ApplicationInformation.Version ||
               parameters.Information.Assembly != ApplicationInformation.Assembly;
    }

    /// <summary>
    /// Проверяет, что версия конфигурации ниже указанной пары Version.Assembly.
    /// </summary>
    private static bool IsBelowVersion(Parameters parameters, int version, int assembly)
    {
        if (parameters.Information.Version < version)
            return true;

        if (parameters.Information.Version > version)
            return false;

        return parameters.Information.Assembly < assembly;
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
