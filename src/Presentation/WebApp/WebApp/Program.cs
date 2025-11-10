using Domain.Configuration.Constants;
using Shared.Installer;

const int ipPort = 2580;
const string appType = "web";
                                                                    
if (args.Contains("--install"))
{
    InstallerFabric.Install(args, 
        $"{ApplicationInformation.Name}-{appType}",
        $"{ApplicationInformation.ServiceName}-{appType}",
        ApplicationInformation.Manufacture,
        ipPort);
}
else if (args.Contains("--uninstall"))
{
    InstallerFabric.Uninstall($"{ApplicationInformation.Name}-{appType}",
        $"{ApplicationInformation.ServiceName}-{appType}", 
        ApplicationInformation.Manufacture, 
        ipPort);
}
else if (args.Contains("--help"))
{
    Console.WriteLine("Использование:");
    Console.WriteLine("--install - для установки службы (для linux - генерация скриптов установки)");
    Console.WriteLine("--uninstall - для удаления службы (для linux - генерация скриптов удаления)");
}

if (args.Length > 0)
    return;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Starting WebApp application...");

builder.WebHost.UseUrls($"http://+:{ipPort}");

builder.Services.AddRazorPages();

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

var app = builder.Build();

Console.WriteLine("Application built successfully");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = prm =>
    {
        prm.Context.Response.Headers.Append("Cache-Control", "publc, max-age=3600");
    }
});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

Console.WriteLine($"Starting server on http://+:{ipPort}");
Console.WriteLine("Press Ctrl+C to stop the server");

await app.RunAsync();
