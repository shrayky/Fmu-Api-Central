using System.Text.Json.Serialization;

namespace Domain.Database;

/// <summary>
/// Итог импорта zip-архива с JSON-пакетами баз.
/// </summary>
public sealed class DatabaseDumpImportResult
{
    [JsonPropertyName("databases")]
    public int Databases { get; init; }

    [JsonPropertyName("packages")]
    public int Packages { get; init; }

    [JsonPropertyName("documents")]
    public int Documents { get; init; }
}
