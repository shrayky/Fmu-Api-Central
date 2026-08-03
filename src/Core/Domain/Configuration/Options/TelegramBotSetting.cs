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

    [JsonPropertyName("localModuleAlerts")]
    public LocalModuleAlertSettings LocalModuleAlerts { get; set; } = new();

    [JsonPropertyName("tsPiotAlerts")]
    public TsPiotAlertSettings TsPiotAlerts { get; set; } = new();

    [JsonPropertyName("scheduler")]
    public List<ScheduleTime> Scheduler { get; set; } = new();

    [JsonPropertyName("alertsInterval")]
    [Obsolete("Устарело: используйте Scheduler. Поле оставлено только для обратной совместимости в коде.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AlertIntervalInMinutes { get; set; } = 60;

    // Устаревшие плоские поля — читаются при миграции, в новый JSON не пишутся
    [JsonPropertyName("localModuleVersionAlert")]
    [Obsolete("Устарело: используйте LocalModuleAlerts.VersionAlert.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyLocalModuleVersionAlert { get; set; }

    [JsonPropertyName("localModuleDaysWithoutSynchronization")]
    [Obsolete("Устарело: используйте LocalModuleAlerts.DaysWithoutSynchronization.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyLocalModuleDaysWithoutSynchronization { get; set; }

    [JsonPropertyName("tsPiotStatusAlertEnabled")]
    [Obsolete("Устарело: используйте TsPiotAlerts.StatusAlertEnabled.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyTsPiotStatusAlertEnabled { get; set; }

    [JsonPropertyName("tsPiotLicenseAlertEnabled")]
    [Obsolete("Устарело: используйте TsPiotAlerts.LicenseAlertEnabled.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyTsPiotLicenseAlertEnabled { get; set; }

    [JsonPropertyName("tsPiotLicenseAlertDays")]
    [Obsolete("Устарело: используйте TsPiotAlerts.LicenseAlertDays.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyTsPiotLicenseAlertDays { get; set; }

    [JsonPropertyName("tsPiotVersionAlert")]
    [Obsolete("Устарело: используйте TsPiotAlerts.VersionAlert.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyTsPiotVersionAlert { get; set; }
}

public record LocalModuleAlertSettings
{
    [JsonPropertyName("versionAlert")]
    public string VersionAlert { get; set; } = string.Empty;

    [JsonPropertyName("daysWithoutSynchronization")]
    public int DaysWithoutSynchronization { get; set; } = 3;
}

public record TsPiotAlertSettings
{
    [JsonPropertyName("statusAlertEnabled")]
    public bool StatusAlertEnabled { get; set; } = false;

    [JsonPropertyName("licenseAlertEnabled")]
    public bool LicenseAlertEnabled { get; set; } = false;

    [JsonPropertyName("licenseAlertDays")]
    public int LicenseAlertDays { get; set; } = 7;

    [JsonPropertyName("versionAlert")]
    public string VersionAlert { get; set; } = string.Empty;
}

public record ScheduleTime
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("time")]
    public TimeOnly Time { get; set; }
}
