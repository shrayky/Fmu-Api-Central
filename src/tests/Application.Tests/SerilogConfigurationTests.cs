using Serilog.Core;
using Shared.Logging;

namespace Application.Tests;

public class SerilogConfigurationTests
{
    [Fact]
    public void LogToFile_при_debug_не_пишет_цикл_очистки_HttpMessageHandler()
    {
        var folder = Directory.CreateTempSubdirectory("fmu-log-");
        var path = Path.Combine(folder.FullName, "app.log");
        var logger = SerilogConfiguration.LogToFile("debug", path, 1);

        try
        {
            logger
                .ForContext(Constants.SourceContextPropertyName, "Microsoft.Extensions.Http.DefaultHttpClientFactory")
                .Debug("Starting HttpMessageHandler cleanup cycle with 5 items");

            logger
                .ForContext(Constants.SourceContextPropertyName, "App.Feature")
                .Debug("полезное сообщение");
        }
        finally
        {
            (logger as IDisposable)?.Dispose();
        }

        try
        {
            var text = string.Concat(Directory.GetFiles(folder.FullName).Select(File.ReadAllText));
            Assert.DoesNotContain("HttpMessageHandler cleanup cycle", text);
            Assert.Contains("полезное сообщение", text);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
