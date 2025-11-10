using System.Reflection;
using Domain.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TelegramBot.Workers;

namespace TelegramBot.Extensions;

public static class Registration
{
    public static IServiceCollection AddTelegramBot(this IServiceCollection services)
    {
        services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);
        
        services.AddHostedService<TelegramMessagingWorker>();
        
        return services;
    } 
}