using CSharpFunctionalExtensions;

namespace Domain.Configuration.Interfaces
{
    public interface IConfigurationFileManager
    {
        Task<Result<Parameters>> LoadConfiguration();
        Task SaveConfiguration(Parameters parameters);
        Task CreateBackup(Parameters parameters);
        Task<Result<Parameters>> RestoreFromBackup();
        bool ConfigurationExists();
    }
}
