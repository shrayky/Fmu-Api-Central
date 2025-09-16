namespace Domain.Database.Interfaces
{
    public interface IDbHealthService
    {
        Task<bool> IsDatabaseEnabled();
        Task<bool> IsDatabaseAccessible(string databaseName);
        Task<Dictionary<string, bool>> GetAllDatabasesStatus();
        Task<bool> IsConnectionHealthy();
    }
}
