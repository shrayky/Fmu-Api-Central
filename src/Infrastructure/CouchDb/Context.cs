using CouchDb.DatabaseScheme;
using CouchDb.Dto;
using CouchDB.Driver;
using Domain.Entitys;
using Domain.Entitys.Instance;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.Organization;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.SettingsSchema;
using Domain.Entitys.CrptViolations;
using Domain.GisMt.Entity;
using Domain.Entitys.SoftwareUpdateFiles;
using Domain.Etrn;
using Domain.Vetis;

namespace CouchDb;

public class Context
{
    public ICouchDatabase<UniversalDocument<UserEntity>> Users { get; }
    public ICouchDatabase<UniversalDocument<InstanceEntity>> FmuApiInstances { get; }
    public ICouchDatabase<UniversalDocument<InstanceGroupEntity>> InstanceGroups { get; }
    public ICouchDatabase<UniversalDocument<SettingsSchemaEntity>> SettingsSchemas { get; }
    public ICouchDatabase<UniversalDocument<OrganizationEntity>> Organizations { get; }
    public ICouchDatabase<UniversalDocument<SoftwareUpdateFilesEntity>> SoftwareUpdateFiles { get; }
    public ICouchDatabase<UniversalDocument<MarkCheckStatisticsEntity>> MarkCheckStatistics { get; }
    public ICouchDatabase<UniversalDocument<GisMtDocumentEntity>> GisMtDocuments { get; }
    public ICouchDatabase<UniversalDocument<GisMtMarkEntity>> GisMtMarks { get; }
    public ICouchDatabase<UniversalDocument<AlertTemplateEntity>> AlertTemplates { get; }
    public ICouchDatabase<UniversalDocument<CrptViolationsDailyEntity>> CrptViolations { get; }
    public ICouchDatabase<UniversalDocument<VetisIncomingDocumentEntity>> VetisIncoming { get; }
    public ICouchDatabase<UniversalDocument<VetisApplicationEntity>> VetisApplications { get; }
    public ICouchDatabase<UniversalDocument<EtrnIncomingDocumentEntity>> EtrnIncoming { get; }
    public ICouchDatabase<UniversalDocument<EtrnSyncState>> EtrnSyncStates { get; }
    public ICouchDatabase<UniversalDocument<EtrnRequestEntity>> EtrnRequests { get; }

    public Context(CouchClient client)
    {
        Users = client.GetDatabase<UniversalDocument<UserEntity>>(DatabaseNames.Users);
        FmuApiInstances = client.GetDatabase<UniversalDocument<InstanceEntity>>(DatabaseNames.Instance);
        InstanceGroups = client.GetDatabase<UniversalDocument<InstanceGroupEntity>>(DatabaseNames.InstanceGroup);
        SettingsSchemas = client.GetDatabase<UniversalDocument<SettingsSchemaEntity>>(DatabaseNames.SettingsSchema);
        Organizations = client.GetDatabase<UniversalDocument<OrganizationEntity>>(DatabaseNames.Organization);
        SoftwareUpdateFiles = client.GetDatabase<UniversalDocument<SoftwareUpdateFilesEntity>>(DatabaseNames.SoftwareUpdateFiles);
        MarkCheckStatistics = client.GetDatabase<UniversalDocument<MarkCheckStatisticsEntity>>(DatabaseNames.MarkCheckingStatistic);
        GisMtDocuments = client.GetDatabase<UniversalDocument<GisMtDocumentEntity>>(DatabaseNames.GisMtDocuments);
        GisMtMarks = client.GetDatabase<UniversalDocument<GisMtMarkEntity>>(DatabaseNames.GisMtMarks);
        AlertTemplates = client.GetDatabase<UniversalDocument<AlertTemplateEntity>>(DatabaseNames.AlertTemplates);
        CrptViolations = client.GetDatabase<UniversalDocument<CrptViolationsDailyEntity>>(DatabaseNames.CrptViolations);
        VetisIncoming = client.GetDatabase<UniversalDocument<VetisIncomingDocumentEntity>>(DatabaseNames.VetisIncoming);
        VetisApplications = client.GetDatabase<UniversalDocument<VetisApplicationEntity>>(DatabaseNames.VetisApplications);
        EtrnIncoming = client.GetDatabase<UniversalDocument<EtrnIncomingDocumentEntity>>(DatabaseNames.EtrnIncoming);
        EtrnSyncStates = client.GetDatabase<UniversalDocument<EtrnSyncState>>(DatabaseNames.EtrnIncoming);
        EtrnRequests = client.GetDatabase<UniversalDocument<EtrnRequestEntity>>(DatabaseNames.EtrnRequests);
    }
}
