using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class TelegramBotSetting
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = false;
    
    [JsonPropertyName("chatId")]
    public long ChatId { get; set; }
    
    [JsonPropertyName("botToken")]
    public string BotToken { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public BotProvidersEnum Provider { get; set; } = BotProvidersEnum.telegram;

    [JsonPropertyName("offlineNodeAlertInterval")]
    public int OfflineNodeAlertInterval { get; set; } = 12;
    
    [JsonPropertyName("localModuleVersionAlert")]
    public string LocalModuleVersionAlert { get; set; } = string.Empty;
    
    [JsonPropertyName("localModuleDaysWithoutSynchronization")]
    public int LocalModuleDaysWithoutSynchronization { get; set; } = 3;

    [JsonPropertyName("scheduler")]
    public List<Schedule> Scheduler { get; set; } = new();

    [JsonPropertyName("alertsInterval")]
    [Obsolete("Устарело: используйте Scheduler. Поле оставлено только для обратной совместимости в коде.")]
    [JsonIgnore]
    public int AlertIntervalInMinutes { get; set; } = 60;
}

public record Schedule
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("time")]

    public TimeOnly Time { get; set; }
}