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
