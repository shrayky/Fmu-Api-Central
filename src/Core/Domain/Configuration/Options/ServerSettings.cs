using System.Text.Json.Serialization;

namespace Domain.Configuration.Options
{
    public class ServerSettings
    {
        [JsonPropertyName("apiIpPort")]
        public int ApiIpPort { get; set; } = 2579;
        public bool TsPiotEnabled { get; set; } = false;
        public int LocalModuleVersion { get; set; } = 0;
    }
}
