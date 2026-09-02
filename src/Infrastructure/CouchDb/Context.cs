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
using Domain.GisMt.Entity;
using Domain.Entitys.SoftwareUpdateFiles;

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
    }
}
