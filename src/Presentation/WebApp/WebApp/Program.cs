using Shared.Installer;

const int ipPort = 2580;

if (args.Contains("--help"))
{
    Console.WriteLine("Использование:");
    Console.WriteLine("--service - запуск в режиме службы (рабочий режим под host)");
    Console.WriteLine("--install - установка службы (через fmu-api-central.exe)");
    Console.WriteLine("--uninstall - удаление службы");
    return;
}

if (HostProcessLauncher.IsHostCommand(args))
    Environment.Exit(HostProcessLauncher.Run(args));

if (args.Length > 0 && !args.Contains("--service"))
    return;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Starting WebApp application...");

builder.WebHost.UseUrls($"http://+:{ipPort}");

builder.Services.AddRazorPages();

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
