using CertificateSigning;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;

namespace EtrnOfd;

public static class EtrnOfdRegistration
{
    public static IServiceCollection AddEtrnOfd(this IServiceCollection services)
    {
        services.AddHttpClient(OfdEtrnGateway.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<OfdTokenClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddOrganizationCertificates();
        services.AddSingleton<IOfdAttachedSignature, OfdAttachedSignature>();
        services.AddSingleton<OfdTokenStore>();
        services.AddScoped<IEtrnOperator, OfdEtrnGateway>();
        return services;
    }
}
