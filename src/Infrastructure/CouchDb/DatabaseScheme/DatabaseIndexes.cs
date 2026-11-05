using CouchDb.Models;
using CouchDb.Repositories;

namespace CouchDb.DatabaseScheme;

public class DatabaseIndexes
{
    public static Dictionary<string, CouchDbIndexDefinition[]> DatabaseIndexSchema()
    {
        return new Dictionary<string, CouchDbIndexDefinition[]>
            {
                { DatabaseNames.Users, UsersIndexSchema() },
                { DatabaseNames.Instance, InstanceIndexSchema() },
                { DatabaseNames.InstanceGroup, InstanceGroupIndexSchema() },
                { DatabaseNames.SettingsSchema, SettingsSchemaIndexSchema() },
                { DatabaseNames.Organization, OrganizationIndexSchema() },
                { DatabaseNames.SoftwareUpdateFiles, SoftwareUpdateFilesSchema() },
                { DatabaseNames.MarkCheckingStatistic, MarkCheckingStatisticIndexSchema() },
                { DatabaseNames.GisMtDocuments, GisMtDocumentsIndexSchema() },
                { DatabaseNames.GisMtMarks, GisMtMarksIndexSchema() },
                { DatabaseNames.AlertTemplates, AlertTemplatesIndexSchema() },
                { DatabaseNames.CrptViolations, CrptViolationsIndexSchema() },
                { DatabaseNames.VetisIncoming, VetisIncomingIndexSchema() },
                { DatabaseNames.VetisApplications, VetisApplicationsIndexSchema() },
                { DatabaseNames.EtrnIncoming, EtrnIncomingIndexSchema() },
                { DatabaseNames.EtrnRequests, EtrnRequestsIndexSchema() }
            };
    }

    private static CouchDbIndexDefinition[] EtrnIncomingIndexSchema() =>
        [
            new("id-idx", new(["data.id"]), EtrnIncomingQuery.DesignDoc),
            new("sort-at-idx", new(["data.sortAt"]), EtrnIncomingQuery.DesignDoc),
            new(EtrnIncomingQuery.StaleIndex().Name, new(EtrnIncomingQuery.StaleIndex().Fields), EtrnIncomingQuery.DesignDoc),
            ..EtrnIncomingQuery.FilterIndexes().Select(index =>
                new CouchDbIndexDefinition(index.Name, new(index.Fields), EtrnIncomingQuery.DesignDoc))
        ];

    private static CouchDbIndexDefinition[] EtrnRequestsIndexSchema() =>
        [
            new("created-at-idx", new(["data.createdAt"]), EtrnRequestQuery.DesignDoc),
            new("completed-created-at-idx", new(["data.completed", "data.createdAt"]), EtrnRequestQuery.DesignDoc),
            ..EtrnRequestQuery.FilterIndexes().Select(index =>
                new CouchDbIndexDefinition(index.Name, new(index.Fields), EtrnRequestQuery.DesignDoc))
        ];

    private static CouchDbIndexDefinition[] VetisIncomingIndexSchema() =>
        [
            new("sort-at-idx", new(["data.sortAt"])),
            new("status-id-idx", new(["data.status", "data.id"]), VetisIncomingQuery.DesignDoc),
            new("id-idx", new(["data.id"]), VetisIncomingQuery.DesignDoc),
            new(VetisIncomingQuery.OldestIndex().Name, new(VetisIncomingQuery.OldestIndex().Fields), VetisIncomingQuery.DesignDoc),
            ..VetisIncomingQuery.FilterIndexes().Select(index =>
                new CouchDbIndexDefinition(index.Name, new(index.Fields), VetisIncomingQuery.DesignDoc))
        ];

    private static CouchDbIndexDefinition[] VetisApplicationsIndexSchema() =>
        [
            new("created-at-idx", new(["data.createdAt"])),
            new("completed-created-at-idx", new(["data.completed", "data.createdAt"])),
            ..VetisApplicationQuery.FilterIndexes().Select(index =>
                new CouchDbIndexDefinition(index.Name, new(index.Fields), VetisApplicationQuery.DesignDoc))
        ];

    private static CouchDbIndexDefinition[] UsersIndexSchema() =>
        [
            new("id-idx", new(["data.id"])),
            new("name-idx", new(["data.name"])),
        ];

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
            new("node-id-idx", new(["data.nodeId"])),
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

    private static CouchDbIndexDefinition[] CrptViolationsIndexSchema() =>
        [
            new("inn-idx", new(["data.inn"])),
            new("date-idx", new(["data.date"])),
            new("inn-date-idx", new(["data.inn", "data.date"])),
        ];
}
