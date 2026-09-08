using CSharpFunctionalExtensions;
using Domain.Bot;

namespace Application.Tests.Fakes;

public sealed class FakeMessageService : IMessageService
{
    public FakeMessageService(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public List<(string BotToken, long ChatId, string Message)> Sends { get; } = [];

    public string? LastToken { get; private set; }
    public long LastChatId { get; private set; }
    public string? LastMessage { get; private set; }

    public Task<Result> Send(string botId, long chatId, string message)
    {
        Sends.Add((botId, chatId, message));
        LastToken = botId;
        LastChatId = chatId;
        LastMessage = message;
        return Task.FromResult(Result.Success());
    }
}
