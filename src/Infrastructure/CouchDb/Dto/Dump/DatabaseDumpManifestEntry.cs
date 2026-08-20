using System.Text.Json.Serialization;

namespace CouchDb.Dto.Dump;

/// <summary>
/// Описание выгрузки одной базы в манифесте архива.
/// </summary>
public sealed class DatabaseDumpManifestEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("documentCount")]
    public int DocumentCount { get; set; }

    [JsonPropertyName("packageCount")]
    public int PackageCount { get; set; }
}
