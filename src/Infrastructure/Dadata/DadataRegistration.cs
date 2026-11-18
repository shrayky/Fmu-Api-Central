using System.Net.Http.Headers;
using Domain.Dadata;
using Microsoft.Extensions.DependencyInjection;

namespace Dadata;

/// <summary>
/// Подключает HTTP-клиенты DaData: именованный HttpClient и порт из Domain.
/// </summary>
public static class DadataRegistration
{
    public static IServiceCollection AddDadata(this IServiceCollection services)
    {
        services.AddHttpClient(DadataAddressClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddScoped<IDadataAddressClient, DadataAddressClient>();
        return services;
    }
}
