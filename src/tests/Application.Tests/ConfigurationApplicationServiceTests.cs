using Application.Configuration.Services;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class ConfigurationApplicationServiceTests
{
    /// <summary>
    /// POST настроек не может сбросить passwordConfigured.
    /// </summary>
    [Fact]
    public async Task Update_сохраняет_passwordConfigured()
    {
        var parameters = new FakeParametersService
        {
            CurrentValue = new Parameters()
        };
        parameters.CurrentValue.Security.PasswordConfigured = true;
        var sut = new ConfigurationApplicationService(parameters, NullLogger<ConfigurationApplicationService>.Instance);

        var json = """{"security":{"passwordConfigured":false},"databaseConnection":{"enable":false},"loggerSettings":{},"serverSettings":{}}""";
        var ok = await sut.Update(json);

        Assert.True(ok);
        Assert.True(parameters.CurrentValue.Security.PasswordConfigured);
    }

    /// <summary>
    /// Флаг старого агентского API можно выключить из настроек.
    /// </summary>
    [Fact]
    public async Task Update_принимает_allowLegacyAgentApi()
    {
        var parameters = new FakeParametersService
        {
            CurrentValue = new Parameters()
        };
        parameters.CurrentValue.Security.AllowLegacyAgentApi = true;
        var sut = new ConfigurationApplicationService(parameters, NullLogger<ConfigurationApplicationService>.Instance);

        var json = """{"security":{"allowLegacyAgentApi":false},"databaseConnection":{"enable":false},"loggerSettings":{},"serverSettings":{}}""";
        var ok = await sut.Update(json);

        Assert.True(ok);
        Assert.False(parameters.CurrentValue.Security.AllowLegacyAgentApi);
    }

    /// <summary>
    /// GET отдаёт маску, кэш Parameters остаётся с настоящими секретами.
    /// </summary>
    [Fact]
    public async Task Current_маскирует_пароль_и_botToken()
    {
        var parameters = new FakeParametersService
        {
            CurrentValue = new Parameters()
        };
        parameters.CurrentValue.DatabaseConnection.Password = "couch-secret";
        parameters.CurrentValue.BotSettings.BotToken = "telegram-secret";
        var sut = new ConfigurationApplicationService(parameters, NullLogger<ConfigurationApplicationService>.Instance);

        var json = await sut.Current();

        Assert.DoesNotContain("couch-secret", json);
        Assert.DoesNotContain("telegram-secret", json);
        Assert.Contains("\"password\": \"***\"", json);
        Assert.Contains("\"botToken\": \"***\"", json);
        Assert.Equal("couch-secret", parameters.CurrentValue.DatabaseConnection.Password);
        Assert.Equal("telegram-secret", parameters.CurrentValue.BotSettings.BotToken);
    }

    /// <summary>
    /// POST с маской не затирает текущие секреты.
    /// </summary>
    [Fact]
    public async Task Update_не_затирает_секреты_если_маска()
    {
        var parameters = new FakeParametersService
        {
            CurrentValue = new Parameters()
        };
        parameters.CurrentValue.DatabaseConnection.Password = "couch-secret";
        parameters.CurrentValue.BotSettings.BotToken = "telegram-secret";
        var sut = new ConfigurationApplicationService(parameters, NullLogger<ConfigurationApplicationService>.Instance);

        var json = """{"databaseConnection":{"password":"***"},"telegramBotSettings":{"botToken":"***"}}""";
        var ok = await sut.Update(json);

        Assert.True(ok);
        Assert.Equal("couch-secret", parameters.CurrentValue.DatabaseConnection.Password);
        Assert.Equal("telegram-secret", parameters.CurrentValue.BotSettings.BotToken);
    }

    /// <summary>
    /// Явная новая строка перезаписывает секрет.
    /// </summary>
    [Fact]
    public async Task Update_принимает_новый_секрет()
    {
        var parameters = new FakeParametersService
        {
            CurrentValue = new Parameters()
        };
        parameters.CurrentValue.DatabaseConnection.Password = "couch-secret";
        parameters.CurrentValue.BotSettings.BotToken = "telegram-secret";
        var sut = new ConfigurationApplicationService(parameters, NullLogger<ConfigurationApplicationService>.Instance);

        var json = """{"databaseConnection":{"password":"new-couch"},"telegramBotSettings":{"botToken":"new-bot"}}""";
        var ok = await sut.Update(json);

        Assert.True(ok);
        Assert.Equal("new-couch", parameters.CurrentValue.DatabaseConnection.Password);
        Assert.Equal("new-bot", parameters.CurrentValue.BotSettings.BotToken);
    }

    private sealed class FakeParametersService : IParametersService
    {
        public Parameters CurrentValue { get; set; } = new();

        public Task<Parameters> Current() => Task.FromResult(CurrentValue);

        public Task<bool> Update(Parameters parameters)
        {
            CurrentValue = parameters;
            return Task.FromResult(true);
        }
    }
}
