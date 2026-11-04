using System.Text.Json;
using System.Text.Json.Nodes;
using CouchDb.Dto;
using Domain.Entitys.InstanceGroup;

namespace CouchDb.Tests;

/// <summary>
/// CouchDB удаляет вложения, если в PUT нет блока _attachments.
/// Обновление документа обязано возвращать заглушки, иначе шаблон настроек группы пропадает.
/// </summary>
public class UniversalDocumentAttachmentsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Заглушки_вложений_пишутся_в_документ()
    {
        var document = Document(new Dictionary<string, DocumentAttachmentStub>
        {
            ["settings.json"] = new("application/json", 4, "md5-cRO40WjQnwX0rSW17VEJ8g==", 12609)
        });

        var json = JsonNode.Parse(JsonSerializer.Serialize(document, JsonOptions))!;
        var attachment = json["_attachments"]!["settings.json"]!;

        Assert.Equal("application/json", attachment["content_type"]!.GetValue<string>());
        Assert.Equal(4, attachment["revpos"]!.GetValue<int>());
        Assert.Equal(12609, attachment["length"]!.GetValue<long>());
        Assert.True(attachment["stub"]!.GetValue<bool>());
    }

    [Fact]
    public void Без_заглушек_блока_вложений_нет()
    {
        var json = JsonSerializer.Serialize(Document(null), JsonOptions);

        Assert.DoesNotContain("_attachments", json);
    }

    private static UniversalDocument<InstanceGroupEntity> Document(
        Dictionary<string, DocumentAttachmentStub>? attachments) => new()
        {
            Id = "g1",
            Data = new InstanceGroupEntity { Id = "g1", Name = "Группа" },
            Attachments = attachments
        };
}
