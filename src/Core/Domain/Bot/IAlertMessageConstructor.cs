using Domain.Configuration.Options;

namespace Domain.Bot;

public interface IAlertMessageConstructor
{
    Task<bool> SendNodesStatus(TelegramBotSetting bot);
}
