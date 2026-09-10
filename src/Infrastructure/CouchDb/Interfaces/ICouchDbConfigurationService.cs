using CSharpFunctionalExtensions;
using Domain.Configuration.Options;

namespace CouchDb.Interfaces;

/// <summary>
/// Сверяет _config ноды CouchDB с настройками приложения и исправляет расхождения.
/// </summary>
public interface ICouchDbConfigurationService
{
    Task<Result> EnsureSettings(DatabaseConnection connection, CancellationToken cancellationToken);
}
