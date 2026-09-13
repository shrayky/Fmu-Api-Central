using System.Text.RegularExpressions;

namespace CouchDb.Repositories;

/// <summary>
/// CouchDB.NET не транслирует StartsWith в mango — префикс id только через $regex.
/// </summary>
public static class MarkCheckStatisticsDeleteQuery
{
    public static Dictionary<string, object> ForNode(string nodeId, int limit)
    {
        var pattern = $"^{Regex.Escape(nodeId)}_";

        return new Dictionary<string, object>
        {
            ["selector"] = new Dictionary<string, object>
            {
                ["$or"] =
                    new object[]
                    {
                        new Dictionary<string, object> { ["data.nodeId"] = nodeId },
                        new Dictionary<string, object>
                        {
                            ["_id"] = new Dictionary<string, object> { ["$regex"] = pattern }
                        },
                        new Dictionary<string, object>
                        {
                            ["data.id"] = new Dictionary<string, object> { ["$regex"] = pattern }
                        }
                    }
            },
            ["limit"] = limit
        };
    }

    public static string DocumentId(string couchId, string? dataId)
        => !string.IsNullOrEmpty(couchId) ? couchId : dataId ?? string.Empty;
}
