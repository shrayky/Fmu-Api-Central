using Domain.Etrn;

namespace CouchDb.Repositories;

public static class EtrnIncomingQuery
{
    public const string DesignDoc = "etrn-incoming";

    private const string ConnectionField = "data.operator.connectionId";

    private static readonly string[] FilterFields = ["organizationId", "operator.connectionId", "status"];

    // Список и карточка XML титула не отдают: он нужен только для отправки.
    private static readonly string[] ListFields =
    [
        "_id", "_rev", "data.id", "data.organizationId", "data.operatorDocumentId", "data.operator",
        "data.number", "data.date", "data.consignor", "data.uid", "data.deliveryAddress", "data.status",
        "data.action", "data.lastError", "data.sortAt"
    ];

    private static readonly string[] FullFields = [.. ListFields, "data.lines", "data.generalComment", "data.signedAt", "data.sendingAt", "data.sendingAtUnix"];

    /// <summary>
    /// Индексы для каждого сочетания отборов: равенства стоят перед data.sortAt, иначе CouchDB отвечает no_usable_index.
    /// </summary>
    public static IEnumerable<(string Name, string[] Fields)> FilterIndexes()
    {
        for (var mask = 1; mask < 1 << FilterFields.Length; mask++)
        {
            var fields = FilterFields.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            yield return (IndexName(fields), fields.Select(Field).Append("data.sortAt").ToArray());
        }
    }

    public static Dictionary<string, object> List(EtrnIncomingFilter filter, int skip, int limit)
    {
        var selector = new Dictionary<string, object>
        {
            ["data.sortAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        };
        var sort = new List<Dictionary<string, string>>();
        var filtered = new List<string>();

        AddFilter(selector, sort, filtered, "organizationId", filter.OrganizationId);
        AddFilter(selector, sort, filtered, "operator.connectionId", filter.ConnectionId);
        AddFilter(selector, sort, filtered, "status", filter.Status);
        sort.Add(new Dictionary<string, string> { ["data.sortAt"] = "desc" });

        var query = new Dictionary<string, object>
        {
            ["selector"] = selector,
            ["sort"] = sort.ToArray(),
            ["fields"] = ListFields,
            ["skip"] = skip,
            ["limit"] = limit
        };
        if (filtered.Count > 0)
            query["use_index"] = new[] { DesignDoc, IndexName(filtered) };

        return query;
    }

    public static Dictionary<string, object> ById(string id) => new()
    {
        ["selector"] = new Dictionary<string, object> { ["data.id"] = id },
        ["fields"] = FullFields,
        ["use_index"] = new[] { DesignDoc, "id-idx" },
        ["limit"] = 1
    };

    /// <summary>
    /// Зависшие отправки: ровно 10 минут ещё не зависание, поэтому сравнение строгое.
    /// </summary>
    public static Dictionary<string, object> StaleSending(long beforeUnix, int limit) => new()
    {
        ["selector"] = new Dictionary<string, object>
        {
            ["data.status"] = EtrnDocumentStatus.Sending,
            ["data.sendingAtUnix"] = new Dictionary<string, object> { ["$lt"] = beforeUnix }
        },
        ["fields"] = FullFields,
        ["use_index"] = new[] { DesignDoc, "status-sending-at-unix-idx" },
        ["limit"] = limit
    };

    public static (string Name, string[] Fields) StaleIndex() =>
        ("status-sending-at-unix-idx", ["data.status", "data.sendingAtUnix"]);

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

    private static string IndexName(IReadOnlyList<string> fields) =>
        string.Join("-", fields).Replace('.', '-') + "-sort-at-idx";
}
