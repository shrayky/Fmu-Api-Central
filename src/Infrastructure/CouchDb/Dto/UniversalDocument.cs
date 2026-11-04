using System.Text.Json.Serialization;

namespace CouchDb.Dto;

/// <summary>
/// Заглушка вложения CouchDB: ровно те поля, которые сервер вернул в <c>_attachments</c>.
/// Нужна, чтобы обновление документа не удаляло вложения: CouchDB стирает их,
/// если в PUT нет блока <c>_attachments</c>.
/// </summary>
public sealed record DocumentAttachmentStub(
    [property: JsonPropertyName("content_type")] string? ContentType,
    [property: JsonPropertyName("revpos")] int? RevPos,
    [property: JsonPropertyName("digest")] string? Digest,
    [property: JsonPropertyName("length")] long? Length,
    [property: JsonPropertyName("stub")] bool Stub = true);

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

    /// <summary>
    /// Заглушки вложений, которые надо сохранить при обновлении документа.
    /// Пустое значение в документ не пишется.
    /// </summary>
    [JsonPropertyName("_attachments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, DocumentAttachmentStub>? Attachments { get; set; }

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
