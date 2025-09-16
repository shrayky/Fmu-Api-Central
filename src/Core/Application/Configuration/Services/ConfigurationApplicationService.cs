using Application.Configuration.Interfaces;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationApplicationService : IConfigurationApplicationService
    {
        private readonly IParametersService _parametersService;
        private readonly ILogger<ConfigurationApplicationService> _logger;

        public ConfigurationApplicationService(IParametersService parametersService, ILogger<ConfigurationApplicationService> logger)
        {
            _parametersService = parametersService;
            _logger = logger;
        }

        public object AppInformation() => ApplicationInformation.Information();

        public async Task<string> Current()
        {
            var parameters = await _parametersService.Current();

            var packet = new
            {
                Content = parameters
            };

            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, packet, JsonSerializerOptions.Default);

            stream.Position = 0;
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync();
        }

        public async Task<bool> Update(string jsonConfiguration)
        {
            try
            {
                using var stream = new MemoryStream();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(jsonConfiguration);
                await writer.FlushAsync();

                stream.Position = 0;

                var parameters = await JsonSerializer.DeserializeAsync<Parameters>(stream);
                if (parameters != null) return await _parametersService.Update(parameters);
                
                _logger.LogError("Не удалось десериализовать конфигурацию из входящего json");
                
                return false;

            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Ошибка разбора входящего JSON конфигурации");
                return false;
            }
        }
    }
}
