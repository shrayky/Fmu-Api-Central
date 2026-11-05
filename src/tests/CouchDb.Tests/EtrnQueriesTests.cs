using CouchDb.DatabaseScheme;
using CouchDb.Dto;
using CouchDb.Repositories;
using Domain.Etrn;
using System.Text.Json;

namespace CouchDb.Tests;

public class EtrnQueriesTests
{
    [Fact]
    public void Круг_записи_сохраняет_оператора()
    {
        var document = new EtrnIncomingDocumentEntity
        {
            Id = EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d1"),
            OrganizationId = "o1",
            OperatorDocumentId = "d1",
            Operator = new EtrnOperator { ConnectionId = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС Север" },
            Number = "ТН-1",
            Status = EtrnDocumentStatus.Waiting
        };
        var stored = UniversalDocument<EtrnIncomingDocumentEntity>.FromDomain(document, document.Id);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(stored, options);
        var restored = JsonSerializer.Deserialize<UniversalDocument<EtrnIncomingDocumentEntity>>(json, options)!;

        Assert.Equal("c1", restored.Data.Operator.ConnectionId);
        Assert.Equal(EtrnOperatorType.Saby, restored.Data.Operator.Type);
        Assert.Equal("СБИС Север", restored.Data.Operator.Name);
        Assert.Equal(document.Id, restored.Data.Id);
    }

    [Fact]
    public void Список_входящих_не_отдаёт_сырой_XML()
    {
        var query = EtrnIncomingQuery.List(new EtrnIncomingFilter(), 0, 50);

        var fields = Assert.IsType<string[]>(query["fields"]);
        Assert.DoesNotContain("data.rawXml", fields);
        Assert.Contains("data.operator", fields);
    }

    [Fact]
    public void Список_входящих_фильтрует_по_подключению_внутри_оператора()
    {
        var query = EtrnIncomingQuery.List(
            new EtrnIncomingFilter { OrganizationId = "o1", ConnectionId = "c1" },
            0,
            50);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal("o1", selector["data.organizationId"]);
        Assert.Equal("c1", selector["data.operator.connectionId"]);
        var index = Assert.IsType<string[]>(query["use_index"]);
        Assert.Equal("organizationId-operator-connectionId-sort-at-idx", index[1]);
    }

    [Fact]
    public void Зависшие_отправки_сравнивают_время_строго()
    {
        var query = EtrnIncomingQuery.StaleSending(1_760_000_000, 50);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal(EtrnDocumentStatus.Sending, selector["data.status"]);
        var sendingAt = Assert.IsType<Dictionary<string, object>>(selector["data.sendingAtUnix"]);
        Assert.True(sendingAt.ContainsKey("$lt"));
        Assert.False(sendingAt.ContainsKey("$lte"));
    }

    [Fact]
    public void Список_запросов_не_отдаёт_тела_JSON()
    {
        var query = EtrnRequestQuery.List(new EtrnRequestFilter(), 0, 50);

        var fields = Assert.IsType<string[]>(query["fields"]);
        Assert.DoesNotContain("data.requestBody", fields);
        Assert.DoesNotContain("data.responseBody", fields);
        Assert.Contains("data.hasResponse", fields);
    }

    [Fact]
    public void Индексы_ЭТрН_описаны_для_обеих_баз()
    {
        var schema = DatabaseIndexes.DatabaseIndexSchema();

        Assert.Contains(DatabaseNames.EtrnIncoming, schema.Keys);
        Assert.Contains(DatabaseNames.EtrnRequests, schema.Keys);
        Assert.Contains(schema[DatabaseNames.EtrnIncoming], index => index.Name == "status-sending-at-unix-idx");
        Assert.Contains(schema[DatabaseNames.EtrnRequests], index => index.Name == "completed-created-at-idx");
    }

    [Fact]
    public void Обе_базы_ЭТрН_исключены_из_дампа()
    {
        Assert.Contains(DatabaseNames.EtrnIncoming, DatabaseNames.All());
        Assert.Contains(DatabaseNames.EtrnRequests, DatabaseNames.All());
        Assert.Contains(DatabaseNames.EtrnIncoming, DatabaseNames.ExcludedFromExport());
        Assert.Contains(DatabaseNames.EtrnRequests, DatabaseNames.ExcludedFromExport());
    }
}
