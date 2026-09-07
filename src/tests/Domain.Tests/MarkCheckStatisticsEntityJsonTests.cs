using System.Text.Json;
using Domain.Entitys.MarksCheckStatistic;

namespace Domain.Tests;

public class MarkCheckStatisticsEntityJsonTests
{
    /// <summary>
    /// CouchDB читает сущность через System.Text.Json — nodeId должен попасть в объект.
    /// </summary>
    [Fact]
    public void Deserialize_читает_nodeId()
    {
        const string json = """{"id":"shop-1_1757200000","nodeId":"shop-1","date":1757200000,"total":10}""";

        var entity = JsonSerializer.Deserialize<MarkCheckStatisticsEntity>(json, JsonSerializerOptions.Web);

        Assert.NotNull(entity);
        Assert.Equal("shop-1", entity.NodeId);
    }
}
