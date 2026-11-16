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
    public void Список_входящих_ищет_часть_номера_и_поставщика_без_индекса_равенств()
    {
        var query = EtrnIncomingQuery.List(
            new EtrnIncomingFilter { Number = "1.434", Consignor = "Бурен" },
            0,
            50);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        var number = Assert.IsType<Dictionary<string, object>>(selector["data.number"]);
        var consignor = Assert.IsType<Dictionary<string, object>>(selector["data.consignor"]);
        Assert.Equal(@".*1\.434.*", number["$regex"]);
        var pattern = Assert.IsType<string>(consignor["$regex"]);
        Assert.Equal(".*[бБ][уУ][рР][еЕ][нН].*", pattern);
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        Assert.Matches(regex, "ООО буренка");
        Assert.Matches(regex, "ООО БУРЕНКА");
        Assert.False(query.ContainsKey("use_index"));
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
    public void Список_входящих_отбирает_по_торговой_точке()
    {
        var query = EtrnIncomingQuery.List(
            new EtrnIncomingFilter
            {
                OrganizationId = "o1",
                SalePointUid = "p1",
                Status = EtrnDocumentStatus.Waiting
            },
            0,
            50);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal("o1", selector["data.organizationId"]);
        Assert.Equal("p1", selector["data.salePointUid"]);
        Assert.Equal(EtrnDocumentStatus.Waiting, selector["data.status"]);
        var sort = Assert.IsType<Dictionary<string, string>[]>(query["sort"]);
        Assert.Equal(
            ["data.organizationId", "data.salePointUid", "data.status", "data.sortAt"],
            sort.Select(item => item.Keys.Single()));
        Assert.Equal("organizationId-salePointUid-status-sort-at-idx", Assert.IsType<string[]>(query["use_index"])[1]);
        Assert.Contains("data.salePointUid", Assert.IsType<string[]>(query["fields"]));
    }

    [Fact]
    public void Список_входящих_отбирает_без_точки()
    {
        var query = EtrnIncomingQuery.List(
            new EtrnIncomingFilter { OrganizationId = "o1", OnlyWithoutSalePoint = true },
            0,
            50);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal("", selector["data.salePointUid"]);
        Assert.False(selector.ContainsKey("data.status"));
        Assert.Equal("organizationId-salePointUid-sort-at-idx", Assert.IsType<string[]>(query["use_index"])[1]);
    }

    [Fact]
    public void Список_входящих_без_точки_идёт_прежним_индексом()
    {
        var query = EtrnIncomingQuery.List(new EtrnIncomingFilter { OrganizationId = "o1" }, 0, 50);

        Assert.Equal("organizationId-sort-at-idx", Assert.IsType<string[]>(query["use_index"])[1]);
        Assert.False(Assert.IsType<Dictionary<string, object>>(query["selector"]).ContainsKey("data.salePointUid"));
    }

    [Fact]
    public void Обе_базы_ЭТрН_исключены_из_дампа()
    {
        Assert.Contains(DatabaseNames.EtrnIncoming, DatabaseNames.All());
        Assert.Contains(DatabaseNames.EtrnRequests, DatabaseNames.All());
        Assert.Contains(DatabaseNames.EtrnIncoming, DatabaseNames.ExcludedFromExport());
        Assert.Contains(DatabaseNames.EtrnRequests, DatabaseNames.ExcludedFromExport());
    }

    /// <summary>
    /// Точка всегда отбирается вместе с организацией, поэтому индексов без организации быть не должно.
    /// </summary>
    [Fact]
    public void Схема_содержит_индексы_торговой_точки_ЭТрН()
    {
        var schema = DatabaseIndexes.DatabaseIndexSchema();
        var names = schema[DatabaseNames.EtrnIncoming].Select(index => index.Name).ToList();

        Assert.Contains("organizationId-salePointUid-sort-at-idx", names);
        Assert.Contains("organizationId-salePointUid-operator-connectionId-sort-at-idx", names);
        Assert.Contains("organizationId-salePointUid-status-sort-at-idx", names);
        Assert.Contains("organizationId-salePointUid-operator-connectionId-status-sort-at-idx", names);
        Assert.DoesNotContain(names, name => name.StartsWith("salePointUid-"));
    }

    [Fact]
    public void Индекс_торговой_точки_ставит_организацию_первой()
    {
        var indexes = EtrnIncomingQuery.SalePointIndexes().ToList();

        var baseIndex = Assert.Single(indexes, index => index.Name == "organizationId-salePointUid-sort-at-idx");
        Assert.Equal(["data.organizationId", "data.salePointUid", "data.sortAt"], baseIndex.Fields);
        Assert.All(indexes, index => Assert.Equal("data.organizationId", index.Fields[0]));
    }

    [Fact]
    public void Проверка_удаления_идёт_по_индексу_торговой_точки()
    {
        var query = EtrnIncomingQuery.AnyBySalePoint("o1", "p1");

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal("o1", selector["data.organizationId"]);
        Assert.Equal("p1", selector["data.salePointUid"]);
        var anchor = Assert.IsType<Dictionary<string, object>>(selector["data.sortAt"]);
        Assert.Equal(0L, anchor["$gte"]);
        Assert.Equal(1, query["limit"]);
        Assert.Equal(["data.id"], Assert.IsType<string[]>(query["fields"]));
        var index = Assert.IsType<string[]>(query["use_index"]);
        Assert.Equal("etrn-incoming", index[0]);
        Assert.Equal("organizationId-salePointUid-sort-at-idx", index[1]);
    }
}
