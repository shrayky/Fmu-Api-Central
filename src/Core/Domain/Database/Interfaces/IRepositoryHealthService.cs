namespace Domain.Database.Interfaces
{
    public interface IRepositoryHealthService
    {
        Task<bool> HasRecords(string repositoryName);
        Task<int> GetRecordCount(string repositoryName);
        Task<bool> IsRepositoryHealthy(string repositoryName);
        Task<Dictionary<string, int>> GetAllRepositoriesRecordCount();
    }
}
