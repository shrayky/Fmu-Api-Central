using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;

namespace WebApi.Workers
{
    public class AfterStartWorker : BackgroundService
    {
        private readonly ILogger<AfterStartWorker> _logger;
        private readonly IApplicationState _applicationState;

        public AfterStartWorker(IServiceProvider services, ILogger<AfterStartWorker> logger, IApplicationState applicationState)
        {
            _logger = logger;
            _applicationState = applicationState;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogWarning("Служба запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                
                CheckRestartApplication();
            }
        }
        
        private void CheckRestartApplication()
        {
            if (!_applicationState.NeedRestart())
                return;
            
            _logger.LogWarning("Будет произведен перезапуск приложения из-за изменения настроек.");
                    
            Environment.Exit(0);
        }   
    }
}
