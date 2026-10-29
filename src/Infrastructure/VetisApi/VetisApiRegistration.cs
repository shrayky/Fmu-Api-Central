using System.Net.Http.Headers;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace VetisApi;

public static class VetisApiRegistration
{
    public static IServiceCollection AddVetisApi(this IServiceCollection services)
    {
        services.AddHttpClient(VetisGateway.HttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        });
        services.AddScoped<IVetisGateway, VetisGateway>();
        return services;
    }
}
