using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Dto.Interfaces;

public class SoftwareUpdateFilesEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("version")]
    public int Version { get; set; }
    [JsonPropertyName("assembly")]
    public int Assembly { get; set; }
    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = string.Empty;
    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string AsJson() => JsonSerializer.Serialize(this);
    public string UniqId() => $"{this.Version}_{this.Assembly}_{this.Architecture}_{this.Os}";
}