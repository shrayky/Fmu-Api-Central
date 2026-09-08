using Application.Tests.Fakes;
using Domain.Bot;
using Messages.Services;

namespace Application.Tests;

public class MessageServiceFactoryTests
{
    [Fact]
    public void For_telegram_возвращает_telegram()
    {
        var telegram = new FakeMessageService("tg");
        var factory = new MessageServiceFactory(telegram, new FakeMessageService("max"), new FakeMessageService("ntfy"));

        var result = factory.For(BotProvidersEnum.telegram);

        Assert.True(result.IsSuccess);
        Assert.Same(telegram, result.Value);
    }

    [Fact]
    public void For_неизвестный_провайдер_отказывает()
    {
        var factory = new MessageServiceFactory(
            new FakeMessageService("tg"),
            new FakeMessageService("max"),
            new FakeMessageService("ntfy"));

        var result = factory.For((BotProvidersEnum)99);

        Assert.True(result.IsFailure);
        Assert.Contains("Неизвестный провайдер", result.Error);
    }
}
