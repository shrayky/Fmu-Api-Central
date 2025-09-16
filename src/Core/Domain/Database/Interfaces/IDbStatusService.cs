using Domain.Configuration.Options;

namespace Domain.Database.Interfaces
{
    public interface IDbStatusService
    {
        Task<bool> CheckAvailability(string databaseUrl, CancellationToken cancellationToken = default);
        Task<bool> EnsureDatabasesExists(DatabaseConnection connection, string[] databasesNames, CancellationToken cancellationToken);
        Task<bool> EnsureDefaultUserExists(CancellationToken cancellationToken);
    }
}
