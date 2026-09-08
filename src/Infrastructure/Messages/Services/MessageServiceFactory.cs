using CSharpFunctionalExtensions;
using Domain.Bot;

namespace Messages.Services;

public class MessageServiceFactory : IMessageServiceFactory
{
    private readonly Dictionary<BotProvidersEnum, IMessageService> _map;

    public MessageServiceFactory(IMessageService telegram, IMessageService max, IMessageService ntfy)
    {
        _map = new()
        {
            [BotProvidersEnum.telegram] = telegram,
            [BotProvidersEnum.max] = max,
            [BotProvidersEnum.ntfy] = ntfy
        };
    }

    public Result<IMessageService> For(BotProvidersEnum provider)
        => _map.TryGetValue(provider, out var service)
            ? Result.Success(service)
            : Result.Failure<IMessageService>($"Неизвестный провайдер {provider}");
}
