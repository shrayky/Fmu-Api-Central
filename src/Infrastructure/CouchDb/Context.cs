using CouchDb.Dto;
using CouchDB.Driver;
using CouchDB.Driver.Options;
using Domain.Entitys;

namespace CouchDb
{
    public class Context : CouchContext
    {
        public required CouchDatabase<UniversalDocument<UserEntity>> Users { get; set; }
        public required CouchDatabase<UniversalDocument<InstanceEntity>> FmuApiInstances { get; set; }
        public required CouchDatabase<UniversalDocument<SoftwareUpdateFilesEntity>> SoftwareUpdateFiles { get; set; }

        public Context(CouchOptions<Context> options) : base(options)
        {
        }

        protected override void OnConfiguring(CouchOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnDatabaseCreating(CouchDatabaseBuilder databaseBuilder)
        {
            databaseBuilder.Document<UniversalDocument<UserEntity>>().ToDatabase(DatabaseSchema.Users);
            databaseBuilder.Document<UniversalDocument<InstanceEntity>>().ToDatabase(DatabaseSchema.Instance);
            databaseBuilder.Document<UniversalDocument<SoftwareUpdateFilesEntity>>().ToDatabase(DatabaseSchema.SoftwareUpdateFiles);
        }

    }
}
