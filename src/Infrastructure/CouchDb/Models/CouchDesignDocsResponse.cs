using System.Text.Json.Serialization;

namespace CouchDb.Models;

/// <summary>
/// Ответ CouchDB на GET /{db}/_design_docs.
/// </summary>
public sealed class CouchDesignDocsResponse
{
    [JsonPropertyName("total_rows")]
    public int TotalRows { get; set; }
}
