using System.Text.Json.Serialization;

namespace CouchDb.Dto;

public class UniversalDocument<T> where T : class
{
    public string Id { get; set; } = string.Empty;

    public string Rev { get; set; } = string.Empty;

    /// <summary>
    /// Принимает _id из ответа CouchDB. При записи не сериализуется — драйвер сам мапит Id в _id.
    /// </summary>
    [JsonPropertyName("_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string IdFromCouch
    {
        get => Id;
        set
        {
            if (!string.IsNullOrEmpty(value))
                Id = value;
        }
    }

    /// <summary>
    /// Принимает _rev из ответа CouchDB. При записи не сериализуется — драйвер сам мапит Rev в _rev.
    /// </summary>
    [JsonPropertyName("_rev")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string RevFromCouch
    {
        get => Rev;
        set
        {
            if (!string.IsNullOrEmpty(value))
                Rev = value;
        }
    }

    [JsonPropertyName("data")]
    public required T Data { get; set; }

    public T ToDomain() => Data;

    public static UniversalDocument<T> FromDomain(T entity, string? id) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Data = entity
        };
}
