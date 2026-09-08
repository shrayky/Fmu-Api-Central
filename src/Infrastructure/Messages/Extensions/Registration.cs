using Domain.Bot;
using Domain.Configuration.Options;
using Messages.Services;
using Messages.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Messages.Extensions;

public static class Registration
{
    public static IServiceCollection AddBotService(this IServiceCollection services, TelegramBotSetting settings)
    {
        //services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

        services.AddSingleton<TelegramBotService>();
        services.AddSingleton<MaxBotService>();
        services.AddSingleton<NtfyBotService>();
        services.AddSingleton<IMessageServiceFactory>(sp => new MessageServiceFactory(
            sp.GetRequiredService<TelegramBotService>(),
            sp.GetRequiredService<MaxBotService>(),
            sp.GetRequiredService<NtfyBotService>()));

        services.AddScoped<IAlertMessageConstructor, AlertsConstuctor>();

        services.AddHostedService<MessagesSendWorker>();
        services.AddHostedService<AlertTemplateSendWorker>();

        return services;
    }
}
