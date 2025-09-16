using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.FilesFolders;
using Shared.Json;
using System.Text.Json;

namespace Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationFileManager : IConfigurationFileManager
    {
        private readonly ILogger<ConfigurationFileManager> _logger;
        private readonly string _directoryPath;
        private readonly string _configPath;
        private readonly string _configBackUpPath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ConfigurationFileManager(ILogger<ConfigurationFileManager> logger)
        {
            _logger = logger;

            _directoryPath = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            _configPath = Path.Combine(_directoryPath, "config.json");
            _configBackUpPath = Path.Combine(_directoryPath, "config.bkp");
        }

        public async Task SaveConfiguration(Parameters parameters)
        {
            await SaveToFile(parameters, _configPath, "основной конфигурации");
        }

        public async Task CreateBackup(Parameters parameters)
        {
            await SaveToFile(parameters, _configBackUpPath, "резервной копии конфигурации");
        }

        public async Task<Result<Parameters>> LoadConfiguration()
        {
            var mainConfigResult = await LoadFromFile(_configPath, "основной конфигурации");

            if (mainConfigResult.IsSuccess)
                return mainConfigResult;
        
            _logger.LogWarning("Не удалось загрузить основную конфигурацию, пробую восстановить из резервной копии");
            
            var backupConfigResult = await LoadFromFile(_configBackUpPath, "резервной копии конфигурации");
            if (backupConfigResult.IsSuccess)
                return backupConfigResult;

            _logger.LogWarning("Не удалось загрузить ни основную конфигурацию, ни ее резервную копию. Создаю новую конфигурацию");
            return await CreateDefaultConfiguration();
        }

        public async Task<Result<Parameters>> RestoreFromBackup()
        {
            var backupConfigResult = await LoadFromFile(_configBackUpPath, "резервной копии конфигурации");
            if (backupConfigResult.IsSuccess)
                return backupConfigResult;

            _logger.LogWarning("Не удалось загрузить резервную копию конфигурации. Создаю новую конфигурацию");
            return await CreateDefaultConfiguration();
        }

        public bool ConfigurationExists()
        {
            return File.Exists(_configPath);
        }

        private async Task<Result<Parameters>> LoadFromFile(string filePath, string description)
        {
            if (!File.Exists(filePath))
                return Result.Failure<Parameters>($"Файл {description} не найден: {filePath}");

            await _semaphore.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<Parameters>(json, JsonSerializeOptionsProvider.Default());

                if (config == null)
                    return Result.Failure<Parameters>($"Не удалось десериализовать файл {description} {filePath}");

                _logger.LogDebug("Успешно загружен файл конфигурации {Description} из {filePath}", description, filePath);

                return Result.Success(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке {Description} из {filePath}", description, filePath);
                return Result.Failure<Parameters>($"Ошибка при загрузке {description}: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task SaveToFile(Parameters parameters, string filePath, string description)
        {
            await _semaphore.WaitAsync();
            try
            {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (directoryPath != null && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var json = JsonSerializer.Serialize(parameters, JsonSerializeOptionsProvider.Default());
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogDebug("Успешно сохранена {Description}", description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении {Description}", description);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<Result<Parameters>> CreateDefaultConfiguration()
        {
            var defaultConfig = new Parameters();
            await SaveConfiguration(defaultConfig);
            await CreateBackup(defaultConfig);

            return Result.Success(defaultConfig);
        }

    }
}
