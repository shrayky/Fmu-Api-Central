using Domain.Attributes;
using Domain.Configuration.Constants;
using Domain.Configuration.Options;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Shared.FilesFolders;
using Shared.Logging;
using System.Reflection;

namespace Logger
{
    public static class LoggerRegistrationExtension
    {
        public static IServiceCollection AddConfigureLogger(this IServiceCollection services, LogSettings settings)
        {
            if (!settings.IsEnabled)
                return services;

            string logFolder = string.Empty;

            if (OperatingSystem.IsWindows())
            {
                logFolder = Path.Combine(Folders.LogFolder(),
                                         ApplicationInformation.Manufacture,
                                         ApplicationInformation.Name,
                                         "log");
            }
            else if (OperatingSystem.IsLinux())
            {
                logFolder = Path.Combine(Folders.LogFolder(),
                                         ApplicationInformation.Manufacture.ToLower(),
                                         ApplicationInformation.Name.ToLower());
            }

            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            string logFileName = Path.Combine(logFolder, $"{ApplicationInformation.Name.ToLower()}.log");

            services.AddLogging(builder =>
            {
                builder.AddSerilog(SerilogConfiguration.LogToFile(settings.LogLevel, logFileName, settings.LogDepth));
            });

            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            return services;
        }
    }
}
