using Application.Configuration.DTO;
using Application.Configuration.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Json;
using System.Text;
using System.Text.Json;

namespace Application.Configuration.Services;

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
        await JsonSerializer.SerializeAsync(stream, packet, JsonSerializeOptionsProvider.Default());

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

            var parameters = await JsonSerializer.DeserializeAsync<Parameters>(stream, JsonSerializeOptionsProvider.Default());
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

    public async Task<Result<PortableSettingsFile>> ExportPortable(CancellationToken cancellationToken)
    {
        try
        {
            var current = await _parametersService.Current();
            var portable = new PortableSettings
            {
                ExportedAt = DateTime.Now,
                LoggerSettings = current.LoggerSettings,
                TelegramBotSettings = current.BotSettings,
                SoftwareUpdateSettings = current.SoftwareUpdateSettings
            };

            var json = await JsonHelpers.SerializeAsync(portable);
            return Result.Success(new PortableSettingsFile
            {
                FileName = $"fmu-api-central-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                Json = json
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта настроек приложения");
            return Result.Failure<PortableSettingsFile>($"Ошибка экспорта настроек: {ex.Message}");
        }
    }

    public async Task<Result> ImportPortable(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file.Length == 0)
                return Result.Failure("Файл настроек пуст");

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            var json = await reader.ReadToEndAsync(cancellationToken);
            var portable = JsonSerializer.Deserialize<PortableSettings>(json, JsonSerializeOptionsProvider.Default());
            if (portable == null)
                return Result.Failure("Не удалось прочитать файл настроек");

            if (portable.FormatVersion != 0 && portable.FormatVersion != PortableSettings.CurrentFormatVersion)
                return Result.Failure($"Неподдерживаемая версия формата настроек: {portable.FormatVersion}");

            var current = await _parametersService.Current();
            if (portable.LoggerSettings != null)
                current.LoggerSettings = portable.LoggerSettings;
            if (portable.TelegramBotSettings != null)
                current.BotSettings = portable.TelegramBotSettings;
            if (portable.SoftwareUpdateSettings != null)
                current.SoftwareUpdateSettings = portable.SoftwareUpdateSettings;

            var updated = await _parametersService.Update(current);
            if (!updated)
                return Result.Failure("Не удалось сохранить импортированные настройки");

            _logger.LogInformation("Импортированы переносимые настройки приложения");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка импорта настроек приложения");
            return Result.Failure($"Ошибка импорта настроек: {ex.Message}");
        }
    }
}
