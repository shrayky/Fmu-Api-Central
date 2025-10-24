using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Shared.FilesFolders;
using Shared.Json;
using System.Text.Json;

namespace Configuration
{
    public static class ParametersLoader
    {
        public static async Task<Result<Parameters>> LoadFromAppFolder()
        {
            var fileName = configFileName();

            var loadedConfiguration = await ReadConfigurationFile(fileName);

            if (loadedConfiguration != null)
                return Result.Success(loadedConfiguration);

            var backupFileName = configBackupFileName();

            loadedConfiguration = await ReadConfigurationFile(backupFileName);

            if (loadedConfiguration == null)
                return Result.Failure<Parameters>("Не удалось загрузить настройки!");

            File.Delete(fileName);
            File.Copy(backupFileName, fileName);

            return Result.Success(loadedConfiguration);
        }

        private static async Task<Parameters?> ReadConfigurationFile(string fileName)
        {
            SemaphoreSlim _semaphore = new(1, 1);
            Parameters? loadedConfiguration = null;

            await _semaphore.WaitAsync();
            
            try
            {
                if (!File.Exists(fileName))
                    return null;

                await using var fileStream = File.OpenRead(fileName);
                loadedConfiguration = await JsonSerializer.DeserializeAsync<Parameters>(fileStream, JsonSerializeOptionsProvider.Default());
            }
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                _semaphore.Release();
            }

            return loadedConfiguration;
        }

        private static string configFileName()
        {
            var configFolder = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            return Path.Combine(configFolder, "config.json");
        }

        private static string configBackupFileName()
        {
            var configFolder = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            return Path.Combine(configFolder, "config.json");
        }
    }
}
