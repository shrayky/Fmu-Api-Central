using Domain.Configuration.Constants;
using System.Text.Json.Serialization;

namespace Domain.Configuration.Options
{
    public class Information
    {
        [JsonPropertyName("name")]
        public string Name { get; } = ApplicationInformation.Name;
        
        [JsonPropertyName("version")]
        public int Version { get; set; } = ApplicationInformation.Version;
        
        [JsonPropertyName("assembly")]
        public int Assembly { get; set; } = ApplicationInformation.Assembly;
    }
}
