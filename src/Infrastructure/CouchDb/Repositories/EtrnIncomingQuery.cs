using System.Text.RegularExpressions;
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
        "data.number", "data.date", "data.consignor", "data.uid", "data.deliveryAddress", "data.salePointUid",
        "data.status", "data.action", "data.lastError", "data.sortAt"
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

    /// <summary>
    /// Отбор точки идёт только вместе с организацией, поэтому salePointUid не входит в FilterFields:
    /// общий перебор дал бы индексы без организации.
    /// </summary>
    public static IEnumerable<(string Name, string[] Fields)> SalePointIndexes()
    {
        yield return SalePointIndex(["organizationId", "salePointUid"]);
        yield return SalePointIndex(["organizationId", "salePointUid", "operator.connectionId"]);
        yield return SalePointIndex(["organizationId", "salePointUid", "status"]);
        yield return SalePointIndex(["organizationId", "salePointUid", "operator.connectionId", "status"]);
    }

    /// <summary>
    /// Есть ли хоть один документ с этой точкой: проверка перед удалением точки из справочника.
    /// </summary>
    public static Dictionary<string, object> AnyBySalePoint(string organizationId, string salePointUid) => new()
    {
        ["selector"] = new Dictionary<string, object>
        {
            ["data.organizationId"] = organizationId,
            ["data.salePointUid"] = salePointUid,
            // Без условия на поле сортировки CouchDB не берёт индекс по нему.
            ["data.sortAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        },
        ["sort"] = new[]
        {
            new Dictionary<string, string> { ["data.organizationId"] = "desc" },
            new Dictionary<string, string> { ["data.salePointUid"] = "desc" },
            new Dictionary<string, string> { ["data.sortAt"] = "desc" }
        },
        ["fields"] = new[] { "data.id" },
        ["use_index"] = new[] { DesignDoc, SalePointIndex(["organizationId", "salePointUid"]).Name },
        ["limit"] = 1
    };

    private static (string Name, string[] Fields) SalePointIndex(string[] fields) =>
        (IndexName(fields), fields.Select(Field).Append("data.sortAt").ToArray());

    public static Dictionary<string, object> List(EtrnIncomingFilter filter, int skip, int limit)
    {
        if (filter.SalePointUid.Length > 0 || filter.OnlyWithoutSalePoint)
            return BySalePoint(filter, skip, limit);

        var selector = new Dictionary<string, object>
        {
            ["data.sortAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        };
        var sort = new List<Dictionary<string, string>>();
        var filtered = new List<string>();

        AddFilter(selector, sort, filtered, "organizationId", filter.OrganizationId);
        AddFilter(selector, sort, filtered, "operator.connectionId", filter.ConnectionId);
        AddFilter(selector, sort, filtered, "status", filter.Status);
        // Подстрока не входит в индекс равенств: иначе CouchDB отвечает no_usable_index.
        AddContains(selector, "number", filter.Number);
        AddContains(selector, "consignor", filter.Consignor);
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

    /// <summary>
    /// Отбор точки: равенства идут в порядке индекса, организация первая, иначе CouchDB ответит no_usable_index.
    /// </summary>
    private static Dictionary<string, object> BySalePoint(EtrnIncomingFilter filter, int skip, int limit)
    {
        var selector = new Dictionary<string, object>
        {
            ["data.organizationId"] = filter.OrganizationId,
            ["data.salePointUid"] = filter.OnlyWithoutSalePoint ? "" : filter.SalePointUid,
            ["data.sortAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        };
        var fields = new List<string> { "organizationId", "salePointUid" };
        var sort = new List<Dictionary<string, string>>
        {
            new() { ["data.organizationId"] = "desc" },
            new() { ["data.salePointUid"] = "desc" }
        };

        if (filter.ConnectionId.Length > 0)
        {
            selector[ConnectionField] = filter.ConnectionId;
            sort.Add(new Dictionary<string, string> { [ConnectionField] = "desc" });
            fields.Add("operator.connectionId");
        }

        if (filter.Status.Length > 0)
        {
            selector["data.status"] = filter.Status;
            sort.Add(new Dictionary<string, string> { ["data.status"] = "desc" });
            fields.Add("status");
        }

        sort.Add(new Dictionary<string, string> { ["data.sortAt"] = "desc" });
        // Подстрока не входит в индекс равенств: иначе CouchDB отвечает no_usable_index.
        AddContains(selector, "number", filter.Number);
        AddContains(selector, "consignor", filter.Consignor);

        return new Dictionary<string, object>
        {
            ["selector"] = selector,
            ["sort"] = sort.ToArray(),
            ["fields"] = ListFields,
            ["use_index"] = new[] { DesignDoc, IndexName(fields) },
            ["skip"] = skip,
            ["limit"] = limit
        };
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

    private static void AddContains(Dictionary<string, object> selector, string field, string value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0)
            return;

        selector[Field(field)] = new Dictionary<string, object>
        {
            ["$regex"] = ContainsPattern(text)
        };
    }

    /// <summary>
    /// (?i) в CouchDB не складывает кириллицу, поэтому у каждой буквы в шаблоне оба регистра.
    /// </summary>
    private static string ContainsPattern(string text)
    {
        var pattern = new System.Text.StringBuilder(".*");
        foreach (var symbol in text)
        {
            var lower = char.ToLowerInvariant(symbol);
            var upper = char.ToUpperInvariant(symbol);
            if (lower == upper)
            {
                pattern.Append(Regex.Escape(symbol.ToString()));
                continue;
            }

            pattern.Append('[').Append(lower).Append(upper).Append(']');
        }

        pattern.Append(".*");
        return pattern.ToString();
    }

    private static string IndexName(IReadOnlyList<string> fields) =>
        string.Join("-", fields).Replace('.', '-') + "-sort-at-idx";
}
