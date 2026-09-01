using Application;
using Authentication;
using Configuration;
using CouchDb;
using Domain.Configuration;
using Logger;
using Messages.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Scalar.AspNetCore;
using Shared.Installer;
using TrueApiIntegration;
using GisMtExchange;
using WebApi.Workers;

var settingsLoadResult = await ParametersLoader.LoadFromAppFolder();
Parameters appSettings = new();

if (settingsLoadResult.IsSuccess)
    appSettings = settingsLoadResult.Value;

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

builder.WebHost.UseUrls($"http://+:{appSettings.ServerSettings.ApiIpPort}");

builder.Services.AddMemoryCache();
builder.Services.AddJwtAuthentication();

builder.Services.AddConfigurationServices();

builder.Services.AddConfigureLogger(appSettings.LoggerSettings);
builder.Services.AddCouchDb(appSettings.DatabaseConnection);
builder.Services.AddApplicationServices();
builder.Services.AddTrueApiIntegration();
builder.Services.AddGisMtExchange();
builder.Services.AddBotService(appSettings.BotSettings);

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
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition"));
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

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
