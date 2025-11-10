using CouchDb.Repositories;
using CouchDb.Workers;
using CouchDB.Driver.DependencyInjection;
using Domain.Attributes;
using Domain.Configuration.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Reflection;
using Domain.Entitys.Interfaces;
using Domain.Entitys.Users.Interfaces;

namespace CouchDb
{
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
            services.AddScoped<ISoftwareUpdatesRepository, SoftwareUpdateFilesRepository>();
                
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

            if (settings.Enable)
            {
                services.AddHostedService<DatabaseStatusCheckWorker>();
                services.AddHostedService<DatabaseCompactWorker>();
            }

            return services;
        }
    }
}
