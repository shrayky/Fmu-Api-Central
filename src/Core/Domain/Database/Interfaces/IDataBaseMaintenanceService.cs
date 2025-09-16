namespace Domain.Database.Interfaces;

public interface IDataBaseMaintenanceService
{
    Task<bool> CompactDatabase();
}