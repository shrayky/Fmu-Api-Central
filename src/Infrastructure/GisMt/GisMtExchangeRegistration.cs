using Domain.Attributes;
using GisMtExchange.Services;
using GisMtExchange.Workers;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Reflection;

namespace GisMtExchange;

public static class GisMtExchangeRegistration
{
    public static IServiceCollection AddGisMtExchange(this IServiceCollection services)
    {
        services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

        services.AddHttpClient(GisMtClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHostedService<GisMtExchangeWorker>();

        return services;
    }
}
