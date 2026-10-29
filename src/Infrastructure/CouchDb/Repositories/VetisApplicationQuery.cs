using Domain.Vetis;

namespace CouchDb.Repositories;

public static class VetisApplicationQuery
{
    public const string DesignDoc = "vetis-applications";

    private static readonly string[] FilterFields = ["organizationId", "enterpriseGuid"];

    // Запрос и ответ — XML на сотни килобайт: в списке они не нужны.
    private static readonly string[] ListFields =
    [
        "_id", "_rev", "data.id", "data.operation", "data.applicationId", "data.status", "data.completed",
        "data.hasResponse", "data.error", "data.organizationId", "data.enterpriseGuid", "data.parameters", "data.createdAt"
    ];

    private static readonly string[] ExpiredFields = ["_id", "_rev", "data.id", "data.completed", "data.createdAt"];

    /// <summary>
    /// Индексы для каждого сочетания отборов: равенства стоят перед data.createdAt, иначе CouchDB отвечает no_usable_index.
    /// </summary>
    public static IEnumerable<(string Name, string[] Fields)> FilterIndexes()
    {
        for (var mask = 1; mask < 1 << FilterFields.Length; mask++)
        {
            var fields = FilterFields.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            yield return (string.Join("-", fields) + "-created-at-idx", fields.Select(field => "data." + field).Append("data.createdAt").ToArray());
        }
    }

    public static Dictionary<string, object> List(VetisApplicationFilter filter, int skip, int limit)
    {
        var selector = new Dictionary<string, object>
        {
            ["data.createdAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        };
        var sort = new List<Dictionary<string, string>>();
        var filtered = new List<string>();
        AddFilter(selector, sort, filtered, "organizationId", filter.OrganizationId);
        AddFilter(selector, sort, filtered, "enterpriseGuid", filter.EnterpriseGuid);
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
            query["use_index"] = new[] { DesignDoc, string.Join("-", filtered) + "-created-at-idx" };

        return query;
    }

    private static void AddFilter(
        Dictionary<string, object> selector,
        List<Dictionary<string, string>> sort,
        List<string> filtered,
        string field,
        string value)
    {
        if (value.Length == 0)
            return;

        selector["data." + field] = value;
        sort.Add(new Dictionary<string, string> { ["data." + field] = "desc" });
        filtered.Add(field);
    }

    public static Dictionary<string, object> Expired(bool completed, long createdBefore, int limit) =>
        new()
        {
            ["selector"] = new Dictionary<string, object>
            {
                ["data.completed"] = completed,
                ["data.createdAt"] = new Dictionary<string, object> { ["$lt"] = createdBefore }
            },
            ["fields"] = ExpiredFields,
            ["limit"] = limit
        };
}
