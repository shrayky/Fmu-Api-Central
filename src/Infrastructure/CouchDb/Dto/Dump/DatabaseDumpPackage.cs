using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CouchDb.Dto.Dump;

/// <summary>
/// Пакет документов одной базы для экспорта и импорта.
/// </summary>
public sealed class DatabaseDumpPackage
{
    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    [JsonPropertyName("packageIndex")]
    public int PackageIndex { get; set; }

    [JsonPropertyName("documents")]
    public List<JsonObject> Documents { get; set; } = [];
}
