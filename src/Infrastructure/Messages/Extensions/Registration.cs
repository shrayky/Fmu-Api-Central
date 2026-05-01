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

        if (settings.Provider == BotProvidersEnum.telegram)
            services.AddSingleton<IMessageService, TelegramBotService>();
        else if (settings.Provider == BotProvidersEnum.max)
            services.AddSingleton<IMessageService, MaxBotService>();
        else if (settings.Provider == BotProvidersEnum.ntfy)
            services.AddSingleton<IMessageService, NtfyBotService>();

        services.AddHostedService<MessagesSendWorker>();

        return services;
    }
}
