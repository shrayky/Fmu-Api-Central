using Domain.Configuration.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Configuration
{
    public class Parameters
    {
        [JsonPropertyName("information")]
        public Information Information { get; set; } = new();
        
        [JsonPropertyName("databaseConnection")]
        public DatabaseConnection DatabaseConnection { get; set; } = new();
        
        [JsonPropertyName("loggerSettings")]
        public LogSettings LoggerSettings { get; set; } = new();
        
        [JsonPropertyName("serverSettings")]
        public ServerSettings ServerSettings { get; set; } = new();

        [JsonPropertyName("telegramBotSettings")]
        public TelegramBotSetting BotSettings { get; set; } = new();

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
