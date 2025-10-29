using Application;
using Authentication;
using Configuration;
using CouchDb;
using Domain.Configuration;
using Domain.Configuration.Constants;
using Logger;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Scalar.AspNetCore;
using Shared.Installer;
using TelegramBot.Extensions;
using WebApi.Workers;

var settingsLoadResult = await ParametersLoader.LoadFromAppFolder();
Parameters appSettings = new();
const string appType = "api";
                                                                    
if (settingsLoadResult.IsSuccess)                                   
    appSettings = settingsLoadResult.Value;                         

if (args.Contains("--install"))
{
    InstallerFabric.Install(args, 
        $"{ApplicationInformation.Name}-{appType}",
        $"{ApplicationInformation.ServiceName}-{appType}",
        ApplicationInformation.Manufacture,
        appSettings.ServerSettings.ApiIpPort);
}
else if (args.Contains("--uninstall"))
{
    InstallerFabric.Uninstall($"{ApplicationInformation.Name}-{appType}",
        $"{ApplicationInformation.ServiceName}-{appType}", 
        ApplicationInformation.Manufacture, 
        appSettings.ServerSettings.ApiIpPort);
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

builder.WebHost.UseUrls($"http://+:{appSettings.ServerSettings.ApiIpPort}");

builder.Services.AddMemoryCache();
builder.Services.AddJwtAuthentication();

builder.Services.AddConfigurationServices();

builder.Services.AddConfigureLogger(appSettings.LoggerSettings);
builder.Services.AddCouchDb(appSettings.DatabaseConnection);
builder.Services.AddApplicationServices();
builder.Services.AddTelegramBot();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<AfterStartWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

var app = builder.Build();

app.UseCors("AllowWebApp");

app.UseSwagger();
app.UseSwagger(options =>
{
    options.RouteTemplate = "/openapi/{documentName}.json";
});
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
