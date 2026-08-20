using System.Text.Json.Serialization;

namespace CouchDb.Dto.Dump;

/// <summary>
/// Манифест zip-архива выгрузки баз CouchDB.
/// </summary>
public sealed class DatabaseDumpManifest
{
    public const int CurrentFormatVersion = 1;

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; set; }

    [JsonPropertyName("bulkBatchSize")]
    public int BulkBatchSize { get; set; }

    [JsonPropertyName("databases")]
    public List<DatabaseDumpManifestEntry> Databases { get; set; } = [];
}
