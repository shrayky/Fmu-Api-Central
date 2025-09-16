using Application;
using Authentication;
using Configuration;
using CouchDb;
using Domain.Configuration;
using Logger;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Scalar.AspNetCore;
using WebApi.Workers;

var builder = WebApplication.CreateBuilder(args);

var settingsLoadResult = await ParametersLoader.LoadFromAppFolder();
Parameters appSettings = new();

if (settingsLoadResult.IsSuccess)
    appSettings = settingsLoadResult.Value;

builder.WebHost.UseUrls($"http://+:{appSettings.ServerSettings.ApiIpPort}");

builder.Services.AddMemoryCache();
builder.Services.AddJwtAuthentication();

builder.Services.AddConfigurationServices();

builder.Services.AddConfigureLogger(appSettings.LoggerSettings);
builder.Services.AddCouchDb(appSettings.DatabaseConnection);
builder.Services.AddApplicationServices();


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

var app = builder.Build();

app.UseCors("AllowWebApp");

app.UseSwagger();
app.UseSwagger(options =>
{
    options.RouteTemplate = "/openapi/{documentName}.json";
});
app.MapScalarApiReference();

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
