using HostApp;
using HostApp.Installer;
using HostApp.Services;
using HostApp.Workers;
using Serilog;

var command = FirstHostCommand(args);
if (command is not null)
{
    if (!OperatingSystem.IsWindows())
    {
        Console.WriteLine("Установка и удаление службы поддерживаются только в Windows.");
        return 1;
    }

    var installer = new WindowsHostInstaller();
    var exitCode = command switch
    {
        "--install" => await installer.InstallAsync(args),
        "--uninstall" => installer.Uninstall(),
        "--register" => installer.Register(),
        "--unregister" => installer.Unregister(),
        _ => 1
    };

    return exitCode;
}

Directory.CreateDirectory(HostPaths.LogFolder);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(HostPaths.LogFolder, "host-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    Log.Information("Старт HostApp ({Service})", HostConstants.ServiceDisplayName);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = HostConstants.ServiceName;
    });

    builder.Services.AddSerilog();
    builder.Services.AddSingleton<ProductDiscovery>();
    builder.Services.AddSingleton<ChildProcessSupervisor>();
    builder.Services.AddSingleton<VersionCleanup>();
    builder.Services.AddHostedService<ProductsHostWorker>();

    var host = builder.Build();
    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "HostApp аварийно завершился");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Ищет команду установки в аргументах Main и в полной командной строке.
/// </summary>
static string? FirstHostCommand(string[] args)
{
    foreach (var raw in EnumerateArguments(args))
    {
        var a = raw.Trim();
        if (a is "--install" or "--uninstall" or "--register" or "--unregister")
            return a;
    }

    return null;
}

static IEnumerable<string> EnumerateArguments(string[] args)
{
    foreach (var a in args)
        yield return a;

    var commandLine = Environment.GetCommandLineArgs();
    for (var i = 1; i < commandLine.Length; i++)
        yield return commandLine[i];
}
