using System.Text.Json.Serialization;

namespace Domain.Configuration.Options
{
    public class LogSettings
    {
        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;
        
        [JsonPropertyName("logLevel")]
        public string LogLevel { get; set; } = "Warning";
        
        [JsonPropertyName("logDepth")]
        public int LogDepth { get; set; } = 30;
    }
}
