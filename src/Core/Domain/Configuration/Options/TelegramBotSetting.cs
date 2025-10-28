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

    [JsonPropertyName("alertsInterval")] 
    public int AlertIntervalInMinutes { get; set; } = 60;

    [JsonPropertyName("offlineNodeAlertInterval")]
    public int OfflineNodeAlertInterval { get; set; } = 12;
    
    [JsonPropertyName("localModuleVersionAlert")]
    public string LocalModuleVersionAlert { get; set; } = string.Empty;
    
    [JsonPropertyName("localModuleDaysWithoutSynchronization")]
    public int LocalModuleDaysWithoutSynchronization { get; set; } = 3;
}