using Domain.Configuration.Options;
using System.Text.Json.Serialization;

namespace Application.Configuration.DTO;

/// <summary>
/// Переносимые настройки приложения без подключения к БД и параметров сервера.
/// </summary>
public sealed class PortableSettings
{
    public const int CurrentFormatVersion = 1;

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; set; }

    [JsonPropertyName("loggerSettings")]
    public LogSettings? LoggerSettings { get; set; }

    [JsonPropertyName("telegramBotSettings")]
    public TelegramBotSetting? TelegramBotSettings { get; set; }

    [JsonPropertyName("softwareUpdateSettings")]
    public SoftwareUpdateSettings? SoftwareUpdateSettings { get; set; }
}
