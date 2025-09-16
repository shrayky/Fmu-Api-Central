using Domain.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Configuration
{
    public static class ConfigurationRegistrationExtension
    {
        public static IServiceCollection AddConfigurationServices(this IServiceCollection services)
        {
            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            return services;
        }
    }
}
