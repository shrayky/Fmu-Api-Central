namespace Domain.Database.Interfaces
{
    public interface IBaseRepository
    {
        Task<int> RecordCount();
        Task<bool> IsHealthy();
    }
}
