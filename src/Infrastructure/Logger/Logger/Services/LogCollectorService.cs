using Domain.Attributes;
using Domain.Configuration.Constants;
using Domain.Logs.Dto;
using Domain.Logs.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.FilesFolders;

namespace Logger.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class LogCollectorService : ILogCollectorService
    {
        private ILogger<LogCollectorService> _logger;

        private readonly string _logFileName;
        private readonly string _logsFolderPath;

        public LogCollectorService(ILogger<LogCollectorService> logger) 
        { 
            _logger = logger; 

            _logFileName = $"{ApplicationInformation.Name.ToLower()}";

            if (OperatingSystem.IsWindows())
            {
                _logsFolderPath = Path.Combine(Folders.LogFolder(),
                                             ApplicationInformation.Manufacture,
                                             ApplicationInformation.Name,
                                             "log");
            }
            else if (OperatingSystem.IsLinux())
            {
                _logsFolderPath = Path.Combine(Folders.LogFolder(),
                                             ApplicationInformation.Manufacture.ToLower(),
                                             ApplicationInformation.Name.ToLower());
            }
            else
            {
                _logsFolderPath = string.Empty;
            }
        }

        public async Task<LogPacket> Collect()
        {
            return await Collect("");
        }

        public async Task<LogPacket> Collect(string selectedFileName)
        {
            LogPacket packet = new();       

            if (!Directory.Exists(_logsFolderPath))
                return new();

            var files = Directory.EnumerateFiles(_logsFolderPath, $"{_logFileName}*.log");

            if (!files.Any())
                return new();

            string uploadLogFileName = string.Empty;
            string nowFileName = string.Empty;
            string fileNameWithoutPrefix = string.Empty;

            foreach (var file in files)
            {
                fileNameWithoutPrefix = Path.GetFileNameWithoutExtension(file).Replace(_logFileName, "");

                packet.LogFileNames.Add(fileNameWithoutPrefix);

                if (selectedFileName == string.Empty)
                    continue;

                if (fileNameWithoutPrefix == selectedFileName)
                    uploadLogFileName = file;

                nowFileName = file;
            }

            if (selectedFileName == "now")
            {
                uploadLogFileName = nowFileName;
                selectedFileName = fileNameWithoutPrefix;
            }

            packet.LogText = await ReadLogFileAsync(uploadLogFileName);
            packet.SelectedLogFileName = selectedFileName;
            ;

            return packet;

        }

        private async Task<string> ReadLogFileAsync(string logFileName)
        {
            if (string.IsNullOrEmpty(logFileName))
                return string.Empty;

            string tempLog = Path.Combine(Path.GetDirectoryName(logFileName), "temp_slog.txt");

            try
            {
                File.Copy(logFileName, tempLog, true);
            }
            catch
            {
                return string.Empty;
            }

            string log = await File.ReadAllTextAsync(tempLog);

            File.Delete(tempLog);

            return log;
        }
    }
}
