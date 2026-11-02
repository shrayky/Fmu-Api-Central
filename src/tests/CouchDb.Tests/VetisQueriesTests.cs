using CouchDb.DatabaseScheme;
using CouchDb.Repositories;
using Domain.Vetis;

namespace CouchDb.Tests;

public class VetisQueriesTests
{
    [Fact]
    public void Базы_Ветис_не_входят_в_выгрузку_данных()
    {
        Assert.Contains(DatabaseNames.VetisIncoming, DatabaseNames.All());
        Assert.Contains(DatabaseNames.VetisApplications, DatabaseNames.All());
        Assert.Contains(DatabaseNames.VetisIncoming, DatabaseNames.ExcludedFromExport());
        Assert.Contains(DatabaseNames.VetisApplications, DatabaseNames.ExcludedFromExport());
    }

    /// <summary>
    /// Сортировка и отбор без индекса заставили бы CouchDB сканировать всю базу.
    /// </summary>
    [Fact]
    public void Schema_содержит_индексы_Ветис()
    {
        var schema = DatabaseIndexes.DatabaseIndexSchema();

        var incoming = schema[DatabaseNames.VetisIncoming].Single(index => index.Name == "sort-at-idx");
        Assert.Equal(["data.sortAt"], incoming.Index.Fields);

        var applications = schema[DatabaseNames.VetisApplications];
        Assert.Equal(["data.createdAt"], applications.Single(index => index.Name == "created-at-idx").Index.Fields);
        Assert.Equal(
            ["data.completed", "data.createdAt"],
            applications.Single(index => index.Name == "completed-created-at-idx").Index.Fields);
    }

    [Fact]
    public void Incoming_листает_по_убыванию_момента_и_всегда_привязан_к_индексу()
    {
        var query = VetisIncomingQuery.List(new VetisIncomingFilter(), skip: 20, limit: 51);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        var anchor = Assert.IsType<Dictionary<string, object>>(selector["data.sortAt"]);
        Assert.Equal(0L, anchor["$gte"]);
        Assert.Equal(20, query["skip"]);
        Assert.Equal(51, query["limit"]);
        var sort = Assert.IsType<Dictionary<string, string>[]>(query["sort"]);
        Assert.Equal("desc", Assert.Single(sort)["data.sortAt"]);
    }

    [Fact]
    public void Incoming_добавляет_только_заданные_отборы()
    {
        var query = VetisIncomingQuery.List(
            new VetisIncomingFilter { OrganizationId = "o1", EnterpriseGuid = "e1", Status = "accepted" },
            0,
            10);
        var all = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        var empty = Assert.IsType<Dictionary<string, object>>(VetisIncomingQuery.List(new VetisIncomingFilter(), 0, 10)["selector"]);

        Assert.Equal("o1", all["data.organizationId"]);
        Assert.Equal("e1", all["data.enterpriseGuid"]);
        Assert.Equal("accepted", all["data.status"]);
        Assert.Equal(
            new[] { "vetis-incoming", "organizationId-enterpriseGuid-status-sort-at-idx" },
            Assert.IsType<string[]>(query["use_index"]));
        Assert.DoesNotContain("use_index", VetisIncomingQuery.List(new VetisIncomingFilter(), 0, 10).Keys);
        Assert.DoesNotContain("data.organizationId", empty.Keys);
        Assert.DoesNotContain("data.enterpriseGuid", empty.Keys);
        Assert.DoesNotContain("data.status", empty.Keys);
    }

    /// <summary>
    /// Индекс только по моменту CouchDB не использует, если в отборе есть другое поле: _find отвечает no_usable_index.
    /// </summary>
    [Fact]
    public void Incoming_по_статусу_сортирует_индексом_где_статус_перед_моментом()
    {
        var query = VetisIncomingQuery.List(new VetisIncomingFilter { Status = "notAccepted" }, 0, 10);

        var sort = Assert.IsType<Dictionary<string, string>[]>(query["sort"]);
        Assert.Equal("desc", sort[0]["data.status"]);
        Assert.Equal("desc", sort[1]["data.sortAt"]);
        Assert.Equal(new[] { "vetis-incoming", "status-sort-at-idx" }, Assert.IsType<string[]>(query["use_index"]));

        var index = DatabaseIndexes.DatabaseIndexSchema()[DatabaseNames.VetisIncoming]
            .Single(item => item.Name == "status-sort-at-idx");
        Assert.Equal(["data.status", "data.sortAt"], index.Index.Fields);
        Assert.Equal("vetis-incoming", index.Ddoc);
    }

    [Fact]
    public void Incoming_не_читает_ВСД_целиком()
    {
        var fields = Assert.IsType<string[]>(VetisIncomingQuery.List(new VetisIncomingFilter(), 0, 10)["fields"]);

        Assert.Contains("data.id", fields);
        Assert.Contains("data.vsdCount", fields);
        Assert.Contains("data.receivedAt", fields);
        Assert.Contains("data.quenchedBy", fields);
        Assert.Contains("data.quenchedAt", fields);
        Assert.DoesNotContain("data.vsds", fields);

        var ids = Assert.IsType<string[]>(VetisIncomingQuery.EnterpriseIds("o1", "e1", 0, 200)["fields"]);
        Assert.Contains("data.id", ids);
        Assert.DoesNotContain("data.vsds", ids);

        var pending = VetisIncomingQuery.NotAcceptedAfter("doc-9", 2000);
        Assert.False(pending.ContainsKey("skip"));
        var pendingSelector = Assert.IsType<Dictionary<string, object>>(pending["selector"]);
        Assert.Equal("notAccepted", pendingSelector["data.status"]);
        var pendingId = Assert.IsType<Dictionary<string, object>>(pendingSelector["data.id"]);
        Assert.Equal("doc-9", pendingId["$gt"]);
        Assert.Equal(new[] { "vetis-incoming", "status-id-idx" }, Assert.IsType<string[]>(pending["use_index"]));
        Assert.Equal(2000, pending["limit"]);
        var pendingFields = Assert.IsType<string[]>(pending["fields"]);
        Assert.Contains("data.id", pendingFields);
        Assert.DoesNotContain("data.vsds", pendingFields);

        var schema = DatabaseIndexes.DatabaseIndexSchema()[DatabaseNames.VetisIncoming];
        Assert.Equal(
            ["data.status", "data.id"],
            schema.Single(index => index.Name == "status-id-idx").Index.Fields);
    }

    [Fact]
    public void Applications_листает_по_убыванию_времени_без_xml()
    {
        var query = VetisApplicationQuery.List(new VetisApplicationFilter { OrganizationId = "o1" }, skip: 0, limit: 51);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal("o1", selector["data.organizationId"]);
        Assert.DoesNotContain("data.enterpriseGuid", selector.Keys);
        Assert.Contains("data.createdAt", selector.Keys);
        var fields = Assert.IsType<string[]>(query["fields"]);
        Assert.Contains("data.operation", fields);
        Assert.DoesNotContain("data.requestXml", fields);
        Assert.DoesNotContain("data.responseXml", fields);
    }

    [Fact]
    public void Applications_по_организации_и_площадке_сортирует_индексом_перед_временем()
    {
        var byOrganization = VetisApplicationQuery.List(new VetisApplicationFilter { OrganizationId = "o1" }, 0, 10);
        var sort = Assert.IsType<Dictionary<string, string>[]>(byOrganization["sort"]);
        Assert.Equal("desc", sort[0]["data.organizationId"]);
        Assert.Equal("desc", sort[1]["data.createdAt"]);
        Assert.Equal(
            new[] { "vetis-applications", "organizationId-created-at-idx" },
            Assert.IsType<string[]>(byOrganization["use_index"]));

        var both = VetisApplicationQuery.List(
            new VetisApplicationFilter { OrganizationId = "o1", EnterpriseGuid = "e1" },
            0,
            10);
        Assert.Equal(
            new[] { "vetis-applications", "organizationId-enterpriseGuid-created-at-idx" },
            Assert.IsType<string[]>(both["use_index"]));
        Assert.DoesNotContain("use_index", VetisApplicationQuery.List(new VetisApplicationFilter(), 0, 10).Keys);

        var schema = DatabaseIndexes.DatabaseIndexSchema()[DatabaseNames.VetisApplications];
        Assert.Equal(
            ["data.organizationId", "data.createdAt"],
            schema.Single(item => item.Name == "organizationId-created-at-idx").Index.Fields);
        Assert.Equal(
            ["data.enterpriseGuid", "data.createdAt"],
            schema.Single(item => item.Name == "enterpriseGuid-created-at-idx").Index.Fields);
        Assert.Equal(
            ["data.organizationId", "data.enterpriseGuid", "data.createdAt"],
            schema.Single(item => item.Name == "organizationId-enterpriseGuid-created-at-idx").Index.Fields);
        Assert.All(
            new[] { "organizationId-created-at-idx", "enterpriseGuid-created-at-idx", "organizationId-enterpriseGuid-created-at-idx" },
            name => Assert.Equal("vetis-applications", schema.Single(item => item.Name == name).Ddoc));
    }

    [Fact]
    public void Expired_отбирает_по_признаку_завершения_и_времени()
    {
        var query = VetisApplicationQuery.Expired(completed: false, createdBefore: 1000, limit: 200);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        Assert.Equal(false, selector["data.completed"]);
        var created = Assert.IsType<Dictionary<string, object>>(selector["data.createdAt"]);
        Assert.Equal(1000L, created["$lt"]);
        Assert.Equal(200, query["limit"]);
        Assert.DoesNotContain("data.requestXml", Assert.IsType<string[]>(query["fields"]));
    }

    [Fact]
    public void Incoming_для_автогашения_идёт_от_старых_без_skip()
    {
        var first = VetisIncomingQuery.Oldest(organizationId: "o1", status: "notAccepted", dated: true, afterSortAt: 0, afterId: "", limit: 20);

        Assert.False(first.ContainsKey("skip"));
        var selector = Assert.IsType<Dictionary<string, object>>(first["selector"]);
        Assert.Equal("o1", selector["data.organizationId"]);
        Assert.Equal("notAccepted", selector["data.status"]);
        var sortAt = Assert.IsType<Dictionary<string, object>>(selector["data.sortAt"]);
        Assert.Equal(0L, sortAt["$gt"]);
        Assert.False(selector.ContainsKey("$or"));
        var sort = Assert.IsType<Dictionary<string, string>[]>(first["sort"]);
        Assert.Equal("asc", sort[2]["data.sortAt"]);
        Assert.Equal("asc", sort[3]["data.id"]);
        Assert.Equal(new[] { "vetis-incoming", "organizationId-status-sort-at-id-idx" }, Assert.IsType<string[]>(first["use_index"]));
        Assert.DoesNotContain("data.vsds", Assert.IsType<string[]>(first["fields"]));

        var next = VetisIncomingQuery.Oldest("o1", "partial", dated: true, afterSortAt: 100, afterId: "doc-b", limit: 20);
        var nextSelector = Assert.IsType<Dictionary<string, object>>(next["selector"]);
        Assert.True(nextSelector.ContainsKey("$or"));
        Assert.False(next.ContainsKey("skip"));

        var undated = VetisIncomingQuery.Oldest("o1", "notAccepted", dated: false, afterSortAt: 0, afterId: "doc-a", limit: 20);
        var undatedSelector = Assert.IsType<Dictionary<string, object>>(undated["selector"]);
        Assert.Equal(0L, undatedSelector["data.sortAt"]);
        var undatedId = Assert.IsType<Dictionary<string, object>>(undatedSelector["data.id"]);
        Assert.Equal("doc-a", undatedId["$gt"]);

        var index = DatabaseIndexes.DatabaseIndexSchema()[DatabaseNames.VetisIncoming]
            .Single(item => item.Name == "organizationId-status-sort-at-id-idx");
        Assert.Equal(["data.organizationId", "data.status", "data.sortAt", "data.id"], index.Index.Fields);
    }
}
