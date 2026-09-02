using Domain.Configuration.Interfaces;
using Domain.Entitys.AlertTemplates.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messages.Workers;

public class AlertTemplateSendWorker : BackgroundService
{
    private readonly ILogger<AlertTemplateSendWorker> _logger;
    private readonly IParametersService _settings;
    private readonly IServiceScopeFactory _scopeFactory;

    public AlertTemplateSendWorker(
        ILogger<AlertTemplateSendWorker> logger,
        IParametersService settings,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _settings = settings;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
#else
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
#endif

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var bot = (await _settings.Current()).BotSettings;

            if (bot.IsEnabled)
            {
                using var scope = _scopeFactory.CreateScope();
                var runService = scope.ServiceProvider.GetRequiredService<IAlertTemplateRunService>();
                var runResult = await runService.RunDueTemplates(now);

                if (runResult.IsFailure)
                    _logger.LogError("Ошибка запуска шаблонов оповещений: {Error}", runResult.Error);
            }

            var nextMinute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute + 1);
            var delay = nextMinute - DateTime.Now;
            if (delay < TimeSpan.FromSeconds(1))
                delay = TimeSpan.FromSeconds(1);

            await Task.Delay(delay, stoppingToken);
        }
    }
}
