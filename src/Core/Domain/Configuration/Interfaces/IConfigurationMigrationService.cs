using CSharpFunctionalExtensions;

namespace Domain.Configuration.Interfaces
{
    public interface IConfigurationMigrationService
    {
        Task<Parameters> MigrateConfiguration(Parameters parameters);
        bool IsMigrationRequired(Parameters parameters);
        Task<Result<bool>> ValidateConfiguration(Parameters parameters);
    }
}
