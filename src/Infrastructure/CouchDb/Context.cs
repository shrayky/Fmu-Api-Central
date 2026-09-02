using CouchDb.DatabaseScheme;
using CouchDb.Dto;
using CouchDB.Driver;
using CouchDB.Driver.Options;
using Domain.Entitys;
using Domain.Entitys.Instance;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.Organization;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.SettingsSchema;
using Domain.GisMt.Entity;
using Domain.Entitys.SoftwareUpdateFiles;

namespace CouchDb
{
    public class Context : CouchContext
    {
        public required CouchDatabase<UniversalDocument<UserEntity>> Users { get; set; }
        public required CouchDatabase<UniversalDocument<InstanceEntity>> FmuApiInstances { get; set; }
        public required CouchDatabase<UniversalDocument<InstanceGroupEntity>> InstanceGroups { get; set; }
        public required CouchDatabase<UniversalDocument<SettingsSchemaEntity>> SettingsSchemas { get; set; }
        public required CouchDatabase<UniversalDocument<OrganizationEntity>> Organizations { get; set; }
        public required CouchDatabase<UniversalDocument<SoftwareUpdateFilesEntity>> SoftwareUpdateFiles { get; set; }
        public required CouchDatabase<UniversalDocument<MarkCheckStatisticsEntity>> MarkCheckStatistics { get; set; }
        public required CouchDatabase<UniversalDocument<GisMtDocumentEntity>> GisMtDocuments { get; set; }
        public required CouchDatabase<UniversalDocument<GisMtMarkEntity>> GisMtMarks { get; set; }
        public required CouchDatabase<UniversalDocument<AlertTemplateEntity>> AlertTemplates { get; set; }

        public Context(CouchOptions<Context> options) : base(options)
        {
        }

        protected override void OnConfiguring(CouchOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnDatabaseCreating(CouchDatabaseBuilder databaseBuilder)
        {
            databaseBuilder.Document<UniversalDocument<UserEntity>>().ToDatabase(DatabaseNames.Users);
            databaseBuilder.Document<UniversalDocument<InstanceEntity>>().ToDatabase(DatabaseNames.Instance);
            databaseBuilder.Document<UniversalDocument<InstanceGroupEntity>>().ToDatabase(DatabaseNames.InstanceGroup);
            databaseBuilder.Document<UniversalDocument<SettingsSchemaEntity>>().ToDatabase(DatabaseNames.SettingsSchema);
            databaseBuilder.Document<UniversalDocument<OrganizationEntity>>().ToDatabase(DatabaseNames.Organization);
            databaseBuilder.Document<UniversalDocument<SoftwareUpdateFilesEntity>>().ToDatabase(DatabaseNames.SoftwareUpdateFiles);
            databaseBuilder.Document<UniversalDocument<MarkCheckStatisticsEntity>>().ToDatabase(DatabaseNames.MarkCheckingStatistic);
            databaseBuilder.Document<UniversalDocument<GisMtDocumentEntity>>().ToDatabase(DatabaseNames.GisMtDocuments);
            databaseBuilder.Document<UniversalDocument<GisMtMarkEntity>>().ToDatabase(DatabaseNames.GisMtMarks);
            databaseBuilder.Document<UniversalDocument<AlertTemplateEntity>>().ToDatabase(DatabaseNames.AlertTemplates);
        }

    }
}
