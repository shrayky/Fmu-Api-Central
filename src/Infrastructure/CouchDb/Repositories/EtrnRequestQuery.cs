using Domain.Etrn;

namespace CouchDb.Repositories;

public static class EtrnRequestQuery
{
    public const string DesignDoc = "etrn-requests";

    private const string ConnectionField = "data.operator.connectionId";

    private static readonly string[] FilterFields = ["organizationId", "operator.connectionId"];

    // Тела JSON на сотни килобайт: список их не читает.
    private static readonly string[] ListFields =
    [
        "_id", "_rev", "data.id", "data.method", "data.parameters", "data.organizationId", "data.operator",
        "data.completed", "data.hasResponse", "data.error", "data.createdAt"
    ];

    private static readonly string[] ExpiredFields = ["_id", "_rev", "data.id", "data.completed", "data.createdAt"];

    public static IEnumerable<(string Name, string[] Fields)> FilterIndexes()
    {
        for (var mask = 1; mask < 1 << FilterFields.Length; mask++)
        {
            var fields = FilterFields.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            yield return (
                string.Join("-", fields).Replace('.', '-') + "-created-at-idx",
                fields.Select(Field).Append("data.createdAt").ToArray());
        }
    }

    public static Dictionary<string, object> List(EtrnRequestFilter filter, int skip, int limit)
    {
        var selector = new Dictionary<string, object>
        {
            ["data.createdAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        };
        var sort = new List<Dictionary<string, string>>();
        var filtered = new List<string>();

        AddFilter(selector, sort, filtered, "organizationId", filter.OrganizationId);
        AddFilter(selector, sort, filtered, "operator.connectionId", filter.ConnectionId);
        sort.Add(new Dictionary<string, string> { ["data.createdAt"] = "desc" });

        var query = new Dictionary<string, object>
        {
            ["selector"] = selector,
            ["sort"] = sort.ToArray(),
            ["fields"] = ListFields,
            ["skip"] = skip,
            ["limit"] = limit
        };
        if (filtered.Count > 0)
            query["use_index"] = new[] { DesignDoc, string.Join("-", filtered).Replace('.', '-') + "-created-at-idx" };

        return query;
    }

    public static Dictionary<string, object> Expired(bool completed, long createdBefore, int limit) => new()
    {
        ["selector"] = new Dictionary<string, object>
        {
            ["data.completed"] = completed,
            ["data.createdAt"] = new Dictionary<string, object> { ["$lt"] = createdBefore }
        },
        ["fields"] = ExpiredFields,
        ["limit"] = limit
    };

    private static string Field(string field) => field switch
    {
        "operator.connectionId" => ConnectionField,
        _ => "data." + field
    };

    private static void AddFilter(
        Dictionary<string, object> selector,
        List<Dictionary<string, string>> sort,
        List<string> filtered,
        string field,
        string value)
    {
        var text = value ?? "";
        if (text.Length == 0)
            return;

        var fieldName = Field(field);
        selector[fieldName] = text;
        sort.Add(new Dictionary<string, string> { [fieldName] = "desc" });
        filtered.Add(field);
    }
}
