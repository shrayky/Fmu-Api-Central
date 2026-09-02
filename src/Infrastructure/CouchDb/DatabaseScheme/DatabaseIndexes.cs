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
                { DatabaseNames.MarkCheckingStatistic, MarkCheckingStatisticIndexSchema() },
                { DatabaseNames.GisMtDocuments, GisMtDocumentsIndexSchema() },
                { DatabaseNames.GisMtMarks, GisMtMarksIndexSchema() },
                { DatabaseNames.AlertTemplates, AlertTemplatesIndexSchema() }
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

    private static CouchDbIndexDefinition[] GisMtDocumentsIndexSchema() =>
        [
            new("gis-mt-doc-number-idx", new(["data.number"])),
            new("gis-mt-doc-loaded-at-idx", new(["data.loadedAt"])),
        ];

    private static CouchDbIndexDefinition[] GisMtMarksIndexSchema() =>
        [
            new("gis-mt-mark-cis-idx", new(["data.cis"])),
            new("gis-mt-mark-sgtin-idx", new(["data.sGtin"])),
            new("gis-mt-mark-product-group-idx", new(["data.productGroup"])),
            new("gis-mt-mark-product-group-loaded-at-idx", new(["data.productGroup", "data.infoLoadedAt"])),
            new("gis-mt-mark-info-loaded-at-idx", new(["data.infoLoadedAt"])),
            new("gis-mt-mark-cleanup-idx", new(["data.infoLoadedAt", "data.sold"])),
        ];

    private static CouchDbIndexDefinition[] AlertTemplatesIndexSchema() =>
        [
            new("name-idx", new(["data.name"])),
        ];
}
