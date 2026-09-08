using CSharpFunctionalExtensions;

namespace Domain.Bot;

public interface IMessageServiceFactory
{
    Result<IMessageService> For(BotProvidersEnum provider);
}
