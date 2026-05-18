using Domain.Bot;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messages.Workers;

public class MessagesSendWorker : BackgroundService
{
    private readonly ILogger<MessagesSendWorker> _logger;
    private readonly IParametersService _settings;
    private readonly IAlertMessageConstructor _alertMessageConstructor;

    private const int StartDelayMinutes = 1;

    public MessagesSendWorker(ILogger<MessagesSendWorker> logger, IParametersService settings, IAlertMessageConstructor alertMessageConstructor)
    {
        _logger = logger;
        _settings = settings;
        _alertMessageConstructor = alertMessageConstructor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
#else
        await Task.Delay(TimeSpan.FromMinutes(StartDelayMinutes), stoppingToken);
#endif
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await _settings.Current().ConfigureAwait(false);
            var bot = settings.BotSettings;

            if (!bot.IsEnabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                continue;
            }

            var delay = GetDelayToNextSchedule(bot);
            _logger.LogInformation("Следующая отправка сообщений запланирована через {delay}", delay);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            await _alertMessageConstructor.SendNodesStatus(bot);
        }
    }

    private TimeSpan GetDelayToNextSchedule(TelegramBotSetting bot)
    {
        var scheduler = bot.Scheduler;

        if (scheduler.Count == 0)
        {
            _logger.LogWarning("Расписание бота пустое, повторная проверка через 10 минут");
            return TimeSpan.FromMinutes(10);
        }

        var now = DateTime.Now;
        DateTime? nearestRun = null;

        foreach (var item in scheduler)
        {
            var candidateToday = now.Date
                .AddHours(item.Time.Hour)
                .AddMinutes(item.Time.Minute)
                .AddSeconds(item.Time.Second);

            var candidate = candidateToday > now
                ? candidateToday
                : candidateToday.AddDays(1);

            if (nearestRun == null || candidate < nearestRun.Value)
                nearestRun = candidate;
        }

        if (nearestRun == null)
        {
            _logger.LogWarning("Не удалось вычислить ближайшее время расписания, повторная проверка через 10 минут");
            return TimeSpan.FromMinutes(10);
        }
        var delay = nearestRun.Value - now;

        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(10);
    }
}