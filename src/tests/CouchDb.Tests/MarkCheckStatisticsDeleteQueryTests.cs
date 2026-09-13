using CouchDb.Repositories;

namespace CouchDb.Tests;

public class MarkCheckStatisticsDeleteQueryTests
{
    /// <summary>
    /// CouchDB.NET не транслирует StartsWith — префикс id только через $regex.
    /// </summary>
    [Fact]
    public void ForNode_строит_mango_с_regex_по_id()
    {
        var query = MarkCheckStatisticsDeleteQuery.ForNode("shop-1", 50);

        Assert.Equal(50, query["limit"]);

        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        var or = Assert.IsType<object[]>(selector["$or"]);

        Assert.Contains(or, item => HasEquals(item, "data.nodeId", "shop-1"));
        Assert.Contains(or, item => HasRegex(item, "_id", "^shop-1_"));
        Assert.Contains(or, item => HasRegex(item, "data.id", "^shop-1_"));
    }

    [Fact]
    public void ForNode_экранирует_спецсимволы_в_префиксе()
    {
        var query = MarkCheckStatisticsDeleteQuery.ForNode("shop.1+", 10);
        var selector = Assert.IsType<Dictionary<string, object>>(query["selector"]);
        var or = Assert.IsType<object[]>(selector["$or"]);

        Assert.Contains(or, item => HasRegex(item, "_id", @"^shop\.1\+_"));
        Assert.Contains(or, item => HasRegex(item, "data.id", @"^shop\.1\+_"));
    }

    /// <summary>
    /// Удаление идёт по CouchDB _id: у старых документов data.id может быть пустым.
    /// </summary>
    [Fact]
    public void DocumentId_берёт_couch_id()
    {
        Assert.Equal("couch-1", MarkCheckStatisticsDeleteQuery.DocumentId("couch-1", "data-1"));
        Assert.Equal("data-1", MarkCheckStatisticsDeleteQuery.DocumentId("", "data-1"));
    }

    private static bool HasEquals(object item, string field, string value)
    {
        if (item is not Dictionary<string, object> map ||
            !map.TryGetValue(field, out var raw) ||
            raw is not string actual)
            return false;

        return actual == value;
    }

    private static bool HasRegex(object item, string field, string pattern)
    {
        if (item is not Dictionary<string, object> map ||
            !map.TryGetValue(field, out var raw) ||
            raw is not Dictionary<string, object> op ||
            !op.TryGetValue("$regex", out var regex) ||
            regex is not string actual)
            return false;

        return actual == pattern;
    }
}
