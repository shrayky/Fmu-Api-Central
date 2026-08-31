using CouchDb.Models;

namespace CouchDb.DatabaseScheme;

public class DatabaseIndexes
{
    public static Dictionary<string, CouchDbIndexDefinition[]> DatabaseIndexSchema()
    {
        return new Dictionary<string, CouchDbIndexDefinition[]>
            {
                { DatabaseNames.Instance, InstanceIndexSchema() },
                { DatabaseNames.InstanceGroup, InstanceGroupIndexSchema() },
                { DatabaseNames.SettingsSchema, SettingsSchemaIndexSchema() },
                { DatabaseNames.Organization, OrganizationIndexSchema() },
                { DatabaseNames.SoftwareUpdateFiles, SoftwareUpdateFilesSchema() },
                { DatabaseNames.MarkCheckingStatistic, MarkCheckingStatisticIndexSchema() }
            };
    }

    private static CouchDbIndexDefinition[] InstanceIndexSchema() =>
        [
            new("name-idx", new(["data.markId"])),
            new("updated-at-idx", new(["data.updatedAt"])),
            new("group-id-idx", new(["data.groupId"])),
        ];

    private static CouchDbIndexDefinition[] InstanceGroupIndexSchema() =>
        [
            new("name-idx", new(["data.name"])),
            new("settings-schema-id-idx", new(["data.settingsSchemaId"])),
        ];

    private static CouchDbIndexDefinition[] SettingsSchemaIndexSchema() =>
        [
            new("name-idx", new(["data.name"])),
        ];

    private static CouchDbIndexDefinition[] OrganizationIndexSchema() =>
        [
            new("name-idx", new(["data.name"])),
            new("inn-idx", new(["data.inn"])),
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
