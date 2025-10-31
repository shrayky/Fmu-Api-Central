using CSharpFunctionalExtensions;

namespace Domain.Bot;

public interface IMessageService
{
    Task<Result> Send(string botId, long chatId, string message);
}