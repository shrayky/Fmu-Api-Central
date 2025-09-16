using Domain.Configuration.Interfaces;

namespace WebApi.Workers
{
    public class AfterStartWorker : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AfterStartWorker> _logger;
        private readonly IParametersService _settingsService;

        public AfterStartWorker(IServiceProvider services)
        {
            _services = services;
            _logger = _services.GetRequiredService<ILogger<AfterStartWorker>>();
            _settingsService = _services.GetRequiredService<IParametersService>();
            _settingsService.Current();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogWarning("Служба запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
