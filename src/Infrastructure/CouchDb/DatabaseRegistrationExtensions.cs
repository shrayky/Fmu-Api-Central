using CouchDb.Repositories;
using CouchDb.Workers;
using CouchDB.Driver.DependencyInjection;
using Domain.Attributes;
using Domain.Configuration.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Reflection;
using Domain.Entitys.Interfaces;
using Domain.GisMt.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.Organization.Interfaces;
using Domain.Entitys.SettingsSchema.Interfaces;
using Domain.Entitys.Users.Interfaces;

namespace CouchDb;

public static class DatabaseRegistrationExtensions
{
    public static IServiceCollection AddCouchDb(this IServiceCollection services, DatabaseConnection settings)
    {
        services.AddCouchContext<Context>(options =>
        {
            if (settings.Enable)
            {
                options.UseEndpoint(settings.NetAddress);
                options.UseBasicAuthentication(settings.UserName, settings.Password);
            }
            else
            {
                options.UseEndpoint("http://localhost:59841");
                options.UseBasicAuthentication("no", "no");
            }

            options.ConfigureFlurlClient(clientOptions =>
                clientOptions.Timeout = TimeSpan.FromSeconds(settings.QueryTimeout));
        });

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
