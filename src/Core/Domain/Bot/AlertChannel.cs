using System.Text.Json.Serialization;
using Domain.Configuration.Options;

namespace Domain.Bot;

public class AlertChannel
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("provider")]
    [JsonConverter(typeof(JsonStringEnumConverter<BotProvidersEnum>))]
    public BotProvidersEnum Provider { get; set; } = BotProvidersEnum.telegram;

    [JsonPropertyName("chatId")]
    public long ChatId { get; set; }

    [JsonPropertyName("botToken")]
    public string BotToken { get; set; } = string.Empty;

    public static AlertChannel Disabled() => new();

    public static AlertChannel FromBotSettings(TelegramBotSetting bot) => new()
    {
        IsEnabled = bot.IsEnabled,
        Provider = bot.Provider,
        ChatId = bot.ChatId,
        BotToken = bot.BotToken
    };

    /// <summary>
    /// Канал группы только если он включён; иначе запасной глобальный.
    /// </summary>
    public static AlertChannel Resolve(AlertChannel? groupChannel, AlertChannel global)
        => groupChannel is { IsEnabled: true } ? groupChannel : global;
}
