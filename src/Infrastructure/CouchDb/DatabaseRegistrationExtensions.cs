using CouchDb.Repositories;
using CouchDb.Workers;
using CouchDB.Driver;
using CouchDB.Driver.Options;
using Domain.Attributes;
using Domain.Configuration.Options;
using Domain.Entitys.AlertTemplates.Interfaces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.Organization.Interfaces;
using Domain.Entitys.SettingsSchema.Interfaces;
using Domain.Entitys.Users.Interfaces;
using Domain.GisMt.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace CouchDb;

public static class DatabaseRegistrationExtensions
{
    public static IServiceCollection AddCouchDb(this IServiceCollection services, DatabaseConnection settings)
    {
        var endpoint = settings.Enable
            ? settings.NetAddress
            : "http://localhost:59841";

        var userName = settings.Enable
            ? settings.UserName
            : "no";

        var password = settings.Enable
            ? settings.Password
            : "no";

        // HttpClient с таймаутом и отключенной проверкой сертификата — вместо ConfigureFlurlClient из 3.x
        var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            Timeout = TimeSpan.FromSeconds(settings.QueryTimeout)
        };

        var clientOptions = new CouchClientOptions
        {
            HttpClient = httpClient,
            ThrowOnQueryWarning = false,
            JsonSerializerOptions = JsonSerializerOptions.Web
        };

        services.AddSingleton(_ => new CouchClient(
            endpoint,
            new BasicCredentials(userName, password),
            clientOptions));

        services.AddSingleton(provider =>
            new Context(provider.GetRequiredService<CouchClient>()));

        services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

        services.AddScoped<UsersRepository>();
        services.AddScoped<IUserRepository, UsersRepository>();

        services.AddScoped<FmuApiInstancesRepository>();
        services.AddScoped<IInstanceRepository, FmuApiInstancesRepository>();
        services.AddScoped<IInstanceGroupRepository, InstanceGroupsRepository>();
        services.AddScoped<ISettingsSchemaRepository, SettingsSchemaRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<ISoftwareUpdatesRepository, SoftwareUpdateFilesRepository>();
        services.AddScoped<IMarksCheckStatisticRepository, MarkCheckStatisticsRepository>();
        services.AddScoped<IGisMtDocumentRepository, GisMtDocumentRepository>();
        services.AddScoped<IGisMtMarkRepository, GisMtMarkRepository>();
        services.AddScoped<IAlertTemplateRepository, AlertTemplateRepository>();

        services.AddHttpClient("CouchDbState", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddHttpClient("CouchDbCompact", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(10));

        // Без общего таймаута: файл обновления стримится клиенту, обрыв ловит клиентская докачка.
        services.AddHttpClient(SoftwareUpdateFilesRepository.AttachmentHttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        // Выгрузка и загрузка баз идёт пакетами и может занимать дольше обычного queryTimeout.
        services.AddHttpClient(Services.CouchDbDumpService.HttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        if (settings.Enable)
        {
            services.AddHostedService<DatabaseStatusCheckWorker>();
            services.AddHostedService<DatabaseCompactWorker>();
        }

        return services;
    }
}
