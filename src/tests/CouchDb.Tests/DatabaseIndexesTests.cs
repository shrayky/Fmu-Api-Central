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
}
