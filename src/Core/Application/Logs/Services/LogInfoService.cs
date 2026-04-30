using Application.Logs.DTO;
using Application.Logs.Interfaces;
using Domain.Attributes;
using Domain.Logs.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Logs.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class LogInfoService : ILogInfoService
    {
        private readonly ILogCollectorService _logCollectorService;

        public LogInfoService(ILogCollectorService logCollectorService)
        {
            _logCollectorService = logCollectorService;
        }

        public async Task<LogsPacket> Packet(string fileName)
        {
            var data = await _logCollectorService.Collect(fileName);

            LogsPacket packet = new LogsPacket()
            {
                LogFilesNames = data.LogFileNames,
                SelectedLogFileName = data.SelectedLogFileName,
                Text = data.LogText
            };

            return packet;
        }
    }
}
