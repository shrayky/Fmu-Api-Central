using Application;
using Authentication;
using Configuration;
using CouchDb;
using Domain.Configuration;
using Domain.Database;
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
builder.Services.AddScoped<WebApi.Filters.LegacyAgentApiFilter>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Domain.Authentication.AgentAuthClaims.Policy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(Domain.Authentication.AgentAuthClaims.Type, Domain.Authentication.AgentAuthClaims.Agent));

    var usersOnly = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(context => Domain.Authentication.UserAuthPolicy.IsUser(context.User))
        .Build();
    options.DefaultPolicy = usersOnly;
    options.FallbackPolicy = usersOnly;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<AfterStartWorker>();
builder.Services.AddHostedService<LegacyAgentApiDisableWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy => policy
            .WithOrigins(CorsOrigins.Resolve(appSettings.ServerSettings.CorsOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition"));
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = DatabaseDumpLimits.MaxImportBytes;
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
