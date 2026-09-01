using Newtonsoft.Json;

namespace CouchDb.Dto;

/// <summary>
/// Ответ CouchDB POST /{db}/_find.
/// </summary>
public class MangoFindResponse<T>
{
    [JsonProperty("docs")]
    public List<T> Docs { get; set; } = [];
}
