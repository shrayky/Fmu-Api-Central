using Domain.Etrn;
using EtrnSaby.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace EtrnSaby;

public static class EtrnSabyRegistration
{
    public static IServiceCollection AddEtrnSaby(this IServiceCollection services)
    {
        services.AddHttpClient(SabyJsonRpc.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient(EtrnSignatureService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddSingleton<SabySessionStore>();
        services.AddSingleton<SabyJsonRpc>();
        services.AddSingleton<ISabyCertificateDirectory, SabyCertificateDirectory>();
        services.AddSingleton<ISabyAuthenticator, SabyAuthenticator>();
        services.AddSingleton<IEtrnSignaturePort, EtrnSignatureService>();
        services.AddScoped<IEtrnOperator, EtrnSabyGateway>();

        services.AddHostedService<EtrnIncomingWorker>();
        services.AddHostedService<EtrnRequestCleanupWorker>();

        return services;
    }
}
