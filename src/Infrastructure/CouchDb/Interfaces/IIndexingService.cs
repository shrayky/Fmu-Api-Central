using CSharpFunctionalExtensions;
using Domain.Configuration.Options;

namespace CouchDb.Interfaces;

public interface IIndexingService
{
    Task<Result> EnsureIndexesExist(DatabaseConnection connection, CancellationToken  cancellationToken);
}