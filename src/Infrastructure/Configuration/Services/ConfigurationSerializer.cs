// Ignore Spelling: Serializer Deserialize

using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Json;
using System.Text.Json;

namespace Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationSerializer : IConfigurationSerializer
    {
        private readonly ILogger<ConfigurationSerializer> _logger;

        public ConfigurationSerializer(ILogger<ConfigurationSerializer> logger)
        {
            _logger = logger;
        }

        public async Task<Result<Parameters>> DeserializeAsync(string json)
        {
            try
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                var config = await JsonSerializer.DeserializeAsync<Parameters>(stream, JsonSerializeOptionsProvider.Default());
              
                if (config == null)
                    return Result.Failure<Parameters>("Не удалось десериализовать конфигурацию");
                
                return Result.Success(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка десериализации конфигурации");
                return Result.Failure<Parameters>($"Ошибка десериализации: {ex.Message}");
            }
        }

        public async Task<Result<string>> SerializeAsync(Parameters parameters)
        {
            try
            {
                using var stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, parameters, JsonSerializeOptionsProvider.Default());

                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                return Result.Success(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сериализации конфигурации");
                return Result.Failure<string>($"Ошибка сериализации: {ex.Message}");
            }
        }

        public bool IsValidConfiguration(string json)
        {
            try
            {
                JsonSerializer.Deserialize<Parameters>(json, JsonSerializeOptionsProvider.Default());
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
