using Domain.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application
{
    public static class ApplicationServicesRegistrationExtension
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            return services;
        }
    }
}
