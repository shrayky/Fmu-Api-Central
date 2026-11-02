using Domain.Vetis;

namespace CouchDb.Repositories;

public static class VetisIncomingQuery
{
    public const string DesignDoc = "vetis-incoming";

    private static readonly string[] FilterFields = ["organizationId", "enterpriseGuid", "status"];

    // Список читает счётчики, а ВСД с XML открываются только по одному документу.
    private static readonly string[] ListFields =
    [
        "_id", "_rev", "data.id", "data.organizationId", "data.enterpriseGuid", "data.number", "data.date",
        "data.type", "data.loose", "data.consignor", "data.status", "data.sortAt", "data.receivedAt",
        "data.quenchedBy", "data.quenchedAt", "data.vsdCount", "data.acceptedCount"
    ];

    /// <summary>
    /// Индексы для каждого сочетания отборов: равенства стоят перед data.sortAt, иначе CouchDB отвечает no_usable_index.
    /// </summary>
    public static IEnumerable<(string Name, string[] Fields)> FilterIndexes()
    {
        for (var mask = 1; mask < 1 << FilterFields.Length; mask++)
        {
            var fields = FilterFields.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            yield return (IndexName(fields), fields.Select(field => "data." + field).Append("data.sortAt").ToArray());
        }
    }

    public static Dictionary<string, object> List(VetisIncomingFilter filter, int skip, int limit)
    {
        // Без условия на поле сортировки CouchDB не берёт индекс по нему.
        var selector = new Dictionary<string, object>
        {
            ["data.sortAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
        };
        var sort = new List<Dictionary<string, string>>();
        var filtered = new List<string>();

        AddFilter(selector, sort, filtered, "organizationId", filter.OrganizationId);
        AddFilter(selector, sort, filtered, "enterpriseGuid", filter.EnterpriseGuid);
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

    /// <summary>
    /// Только id: XML площадки на этом проходе не нужен, а sortAt до конца чтения не меняется.
    /// </summary>
    public static Dictionary<string, object> EnterpriseIds(string organizationId, string enterpriseGuid, int skip, int limit) =>
        new()
        {
            ["selector"] = new Dictionary<string, object>
            {
                ["data.organizationId"] = organizationId,
                ["data.enterpriseGuid"] = enterpriseGuid,
                ["data.sortAt"] = new Dictionary<string, object> { ["$gte"] = 0L }
            },
            ["sort"] = new[]
            {
                new Dictionary<string, string> { ["data.organizationId"] = "desc" },
                new Dictionary<string, string> { ["data.enterpriseGuid"] = "desc" },
                new Dictionary<string, string> { ["data.sortAt"] = "desc" }
            },
            ["fields"] = new[] { "_id", "data.id" },
            ["use_index"] = new[] { DesignDoc, "organizationId-enterpriseGuid-sort-at-idx" },
            ["skip"] = skip,
            ["limit"] = limit
        };

    /// <summary>
    /// Страница непринятых читается от ключа: `skip` на каждом обновлении фильтра дорожает с каждой страницей.
    /// `data.id` совпадает с `_id`, но у design-документов его нет — по нему курсор их и отсекает.
    /// </summary>
    public static Dictionary<string, object> NotAcceptedAfter(string afterId, int limit) =>
        new()
        {
            ["selector"] = new Dictionary<string, object>
            {
                ["data.status"] = VetisIncomingStatus.NotAccepted,
                ["data.id"] = new Dictionary<string, object> { ["$gt"] = afterId ?? "" }
            },
            ["sort"] = new[]
            {
                new Dictionary<string, string> { ["data.status"] = "asc" },
                new Dictionary<string, string> { ["data.id"] = "asc" }
            },
            ["fields"] = new[] { "data.id", "data.organizationId", "data.enterpriseGuid" },
            ["use_index"] = new[] { DesignDoc, "status-id-idx" },
            ["limit"] = limit
        };

    /// <summary>
    /// Страница журнала по возрастанию `data.id`. Курсор по `_id` не годится: под `$gt` попадают design-документы,
    /// а строка без `data` роняет разбор проекции. У design-документов `data.id` нет.
    /// </summary>
    public static Dictionary<string, object> HeadsAfter(string afterId, int limit) =>
        new()
        {
            ["selector"] = new Dictionary<string, object>
            {
                ["data.id"] = new Dictionary<string, object> { ["$gt"] = afterId ?? "" }
            },
            ["sort"] = new[]
            {
                new Dictionary<string, string> { ["data.id"] = "asc" }
            },
            ["fields"] = new[] { "data.id", "data.date", "data.sortAt" },
            ["use_index"] = new[] { DesignDoc, "id-idx" },
            ["limit"] = limit
        };

    /// <summary>
    /// Страница для автогашения. skip нельзя: гашение меняет статус, и следующая страница с тем же skip пропустит строку.
    /// Продолжение датированных идёт парой (sortAt, id). Документы без даты читаются отдельно, курсор у них только id.
    /// </summary>
    public static Dictionary<string, object> Oldest(
        string organizationId,
        string status,
        bool dated,
        long afterSortAt,
        string afterId,
        int limit)
    {
        var selector = new Dictionary<string, object>
        {
            ["data.organizationId"] = organizationId,
            ["data.status"] = status
        };
        if (dated)
        {
            selector["data.sortAt"] = new Dictionary<string, object> { ["$gt"] = 0L };
            if (afterSortAt > 0 || (afterId ?? "").Length > 0)
            {
                selector["$or"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["data.sortAt"] = new Dictionary<string, object> { ["$gt"] = afterSortAt }
                    },
                    new Dictionary<string, object>
                    {
                        ["data.sortAt"] = afterSortAt,
                        ["data.id"] = new Dictionary<string, object> { ["$gt"] = afterId ?? "" }
                    }
                };
            }
        }
        else
        {
            selector["data.sortAt"] = 0L;
            selector["data.id"] = new Dictionary<string, object> { ["$gt"] = afterId ?? "" };
        }

        return new Dictionary<string, object>
        {
            ["selector"] = selector,
            ["sort"] = new[]
            {
                new Dictionary<string, string> { ["data.organizationId"] = "asc" },
                new Dictionary<string, string> { ["data.status"] = "asc" },
                new Dictionary<string, string> { ["data.sortAt"] = "asc" },
                new Dictionary<string, string> { ["data.id"] = "asc" }
            },
            ["fields"] = new[] { "data.id", "data.date", "data.sortAt" },
            ["use_index"] = new[] { DesignDoc, "organizationId-status-sort-at-id-idx" },
            ["limit"] = limit
        };
    }

    public static (string Name, string[] Fields) OldestIndex() =>
        ("organizationId-status-sort-at-id-idx", ["data.organizationId", "data.status", "data.sortAt", "data.id"]);

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
        // Смешанное направление CouchDB не принимает: все поля индекса сортируются в одну сторону.
        sort.Add(new Dictionary<string, string> { ["data." + field] = "desc" });
        filtered.Add(field);
    }

    private static string IndexName(IReadOnlyList<string> fields) =>
        string.Join("-", fields) + "-sort-at-idx";
}
