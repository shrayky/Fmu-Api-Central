using Domain.Configuration.Options;

namespace Domain.Database.Interfaces;

public interface IIndexingService
{
    Task<bool> EnsureIndexesExist(DatabaseConnection connection, CancellationToken  cancellationToken);
}