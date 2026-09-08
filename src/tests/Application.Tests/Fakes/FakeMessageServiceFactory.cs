using CSharpFunctionalExtensions;
using Domain.Bot;

namespace Application.Tests.Fakes;

public sealed class FakeMessageServiceFactory : IMessageServiceFactory
{
    public Dictionary<BotProvidersEnum, IMessageService> Services { get; } = new();

    public Result<IMessageService> For(BotProvidersEnum provider)
        => Services.TryGetValue(provider, out var service)
            ? Result.Success(service)
            : Result.Failure<IMessageService>($"Неизвестный провайдер {provider}");
}
