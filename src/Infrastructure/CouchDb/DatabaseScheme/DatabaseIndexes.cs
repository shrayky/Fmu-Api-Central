using CouchDb.Models;

namespace CouchDb.DatabaseScheme;

public class DatabaseIndexes
{
    public static Dictionary<string, CouchDbIndexDefinition[]> DatabaseIndexSchema()
    {
        return new Dictionary<string, CouchDbIndexDefinition[]>
            {
                { DatabaseNames.Instance, InstanceIndexSchema() },
                { DatabaseNames.SoftwareUpdateFiles, SoftwareUpdateFilesSchema() },
                { DatabaseNames.MarkCheckingStatistic, MarkCheckingStatisticIndexSchema() }
            };
    }

    private static CouchDbIndexDefinition[] InstanceIndexSchema() =>
        [
            new("name-idx", new(["data.markId"])),
            new("updated-at-idx", new(["data.updatedAt"])),
        ];

    private static CouchDbIndexDefinition[] SoftwareUpdateFilesSchema() =>
        [
            new("updated-at-idx", new(["data.updatedAt"])),

            new("max-update-by-version-assemly-idx", new(["data.os", "data.architecture", "data.version", "data.assembly"])),

            new("max-update-by-version-idx", new(["data.os", "data.architecture", "data.version"]))
        ];

    private static CouchDbIndexDefinition[] MarkCheckingStatisticIndexSchema() =>
        [
            new("date-idx", new(["data.date"])),
        ];
}
