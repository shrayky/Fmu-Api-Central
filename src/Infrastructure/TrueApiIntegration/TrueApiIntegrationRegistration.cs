using Domain.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Reflection;
using TrueApiIntegration.Workers;

namespace TrueApiIntegration;

public static class TrueApiIntegrationRegistration
{
    public static IServiceCollection AddTrueApiIntegration(this IServiceCollection services)
    {
        services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

        services.AddHttpClient("TrueApiIntegration", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHostedService<TrueApiTokenLoaderWorker>();

        return services;
    }
}
