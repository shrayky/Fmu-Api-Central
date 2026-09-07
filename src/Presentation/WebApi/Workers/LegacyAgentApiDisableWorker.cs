using Domain.Authentication;
using Domain.Configuration.Interfaces;
using Domain.Entitys.Interfaces;

namespace WebApi.Workers;

public class LegacyAgentApiDisableWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IParametersService _parameters;
    private readonly ILogger<LegacyAgentApiDisableWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public LegacyAgentApiDisableWorker(
        IServiceScopeFactory scopeFactory,
        IParametersService parameters,
        ILogger<LegacyAgentApiDisableWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _parameters = parameters;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            await TryDisable(stoppingToken);
        }
    }

    private async Task TryDisable(CancellationToken stoppingToken)
    {
        var settings = await _parameters.Current();
        if (!settings.Security.AllowLegacyAgentApi)
            return;

        using var scope = _scopeFactory.CreateScope();
        var instances = scope.ServiceProvider.GetRequiredService<IInstanceRepository>();
        var all = await instances.All();
        if (all.IsFailure)
            return;

        if (!LegacyAgentApiPolicy.ShouldDisable(all.Value, DateTime.UtcNow))
            return;

        settings.Security.AllowLegacyAgentApi = false;
        if (await _parameters.Update(settings))
            _logger.LogWarning("Анонимный API узлов отключён: все агенты перешли на handshake");
    }
}
