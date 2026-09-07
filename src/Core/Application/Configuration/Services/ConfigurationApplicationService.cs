using Application.Configuration.DTO;
using Application.Configuration.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
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
        var forClient = Clone(parameters);
        MaskSecrets(forClient);

        var packet = new
        {
            Content = forClient
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
            if (parameters == null)
            {
                _logger.LogError("Не удалось десериализовать конфигурацию из входящего json");
                return false;
            }

            var current = await _parametersService.Current();
            parameters.Security ??= new();
            current.Security ??= new();
            parameters.Security.PasswordConfigured = current.Security.PasswordConfigured;
            RestoreMaskedSecrets(parameters, current);

            return await _parametersService.Update(parameters);

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
                SoftwareUpdateSettings = current.SoftwareUpdateSettings,
                GisMtSettings = current.GisMtSettings
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
            if (portable.GisMtSettings != null)
                current.GisMtSettings = portable.GisMtSettings;

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

    private static Parameters Clone(Parameters source)
    {
        var json = JsonSerializer.Serialize(source, JsonSerializeOptionsProvider.Default());
        return JsonSerializer.Deserialize<Parameters>(json, JsonSerializeOptionsProvider.Default())
               ?? new Parameters();
    }

    private static void MaskSecrets(Parameters parameters)
    {
        parameters.DatabaseConnection.Password = SecretMask.ForClient(parameters.DatabaseConnection.Password);
        parameters.BotSettings.BotToken = SecretMask.ForClient(parameters.BotSettings.BotToken);
    }

    private static void RestoreMaskedSecrets(Parameters incoming, Parameters current)
    {
        incoming.DatabaseConnection.Password = SecretMask.Restore(
            incoming.DatabaseConnection.Password,
            current.DatabaseConnection.Password);
        incoming.BotSettings.BotToken = SecretMask.Restore(
            incoming.BotSettings.BotToken,
            current.BotSettings.BotToken);
    }
}
