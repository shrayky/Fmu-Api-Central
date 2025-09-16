using System.Text.Json.Serialization;

namespace Domain.Configuration.Options
{
    public class DatabaseConnection
    {
        [JsonPropertyName("enable")]
        public bool Enable { get; set; } = false;
        
        [JsonPropertyName("netAddress")]
        public string NetAddress { get; set; } = string.Empty;
        
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;
        
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        
        [JsonPropertyName("bulkBatchSize")]
        public int BulkBatchSize { get; set; } = 1000;
        
        [JsonPropertyName("bulkParallelTasks")]
        public int BulkParallelTasks { get; set; } = 4;
        [JsonPropertyName("queryLimit")]
        public int QueryLimit { get; set; } = 1000000;
    }
}
