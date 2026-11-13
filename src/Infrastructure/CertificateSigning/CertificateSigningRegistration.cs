using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CertificateSigning;

public static class CertificateSigningRegistration
{
    public static IServiceCollection AddOrganizationCertificates(this IServiceCollection services)
    {
        services.TryAddSingleton<IOrganizationCertificateDirectory, OrganizationCertificateDirectory>();
        return services;
    }
}
