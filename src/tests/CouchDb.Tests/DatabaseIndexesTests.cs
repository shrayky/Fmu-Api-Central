using CouchDb.DatabaseScheme;

namespace CouchDb.Tests;

public class DatabaseIndexesTests
{
    /// <summary>
    /// Поиск и сортировка пользователей идут по data.name — без mango-индекса CouchDB сканирует всю базу.
    /// </summary>
    [Fact]
    public void Schema_содержит_индекс_имени_пользователей()
    {
        var schema = DatabaseIndexes.DatabaseIndexSchema();

        Assert.True(schema.ContainsKey(DatabaseNames.Users));

        var names = schema[DatabaseNames.Users].Select(index => index.Name).ToArray();
        Assert.Contains("name-idx", names);

        var nameIndex = schema[DatabaseNames.Users].Single(index => index.Name == "name-idx");
        Assert.Equal(["data.name"], nameIndex.Index.Fields);
    }

    /// <summary>
    /// Удаление статистики узла ищет по data.nodeId — без mango-индекса CouchDB сканирует всю базу.
    /// </summary>
    [Fact]
    public void Schema_содержит_индекс_узла_статистики_проверок()
    {
        var schema = DatabaseIndexes.DatabaseIndexSchema();

        Assert.True(schema.ContainsKey(DatabaseNames.MarkCheckingStatistic));

        var nodeIndex = schema[DatabaseNames.MarkCheckingStatistic]
            .Single(index => index.Name == "node-id-idx");
        Assert.Equal(["data.nodeId"], nodeIndex.Index.Fields);
    }

    /// <summary>
    /// Поиск отклонений ЧЗ идёт по ИНН и дате — без mango-индекса CouchDB сканирует всю базу.
    /// </summary>
    [Fact]
    public void Schema_содержит_индексы_отклонений_чз()
    {
        var schema = DatabaseIndexes.DatabaseIndexSchema();

        Assert.True(schema.ContainsKey(DatabaseNames.CrptViolations));

        var names = schema[DatabaseNames.CrptViolations].Select(index => index.Name).ToArray();
        Assert.Contains("inn-idx", names);
        Assert.Contains("date-idx", names);
        Assert.Contains("inn-date-idx", names);
    }
}
