using CSharpFunctionalExtensions;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class VetisIncomingRepository : BaseCouchDbRepository<VetisIncomingDocumentEntity>, IVetisIncomingRepository
{
    public VetisIncomingRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().VetisIncoming, services)
    {
    }

    public async Task<Result<VetisIncomingDocumentEntity?>> Find(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<VetisIncomingDocumentEntity?>(DatabaseUnavailable);

        try
        {
            return Result.Success(await GetByIdAsync(id));
        }
        catch (Exception ex)
        {
            return Result.Failure<VetisIncomingDocumentEntity?>($"Ошибка чтения входящего документа Ветис {id}: {ex.Message}");
        }
    }

    public async Task<Result> Save(VetisIncomingDocumentEntity document)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateAsync(document)
                ? Result.Success()
                : Result.Failure($"Не удалось сохранить входящий документ Ветис {document.Id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка сохранения входящего документа Ветис {document.Id}: {ex.Message}");
        }
    }

    public Task<Result> Release(string organizationId, string enterpriseGuid, IReadOnlyCollection<string> uuids, IReadOnlyCollection<string> keepDocumentIds)
    {
        if (!_appState.DbState())
            return Task.FromResult(Result.Failure(DatabaseUnavailable));

        return ReleaseStored(organizationId, enterpriseGuid, uuids, keepDocumentIds);
    }

    private async Task<Result> ReleaseStored(string organizationId, string enterpriseGuid, IReadOnlyCollection<string> uuids, IReadOnlyCollection<string> keepDocumentIds)
    {
        try
        {
            return await VetisIncomingRelease.Apply(
                (skip, limit) => ExecuteMangoQueryAsync(VetisIncomingQuery.EnterpriseIds(organizationId, enterpriseGuid, skip, limit)),
                uuids,
                keepDocumentIds,
                Find,
                Save,
                Delete,
                pageSize: 200);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка переноса ВСД между документами: {ex.Message}");
        }
    }

    private async Task<Result> Delete(string id)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await DeleteAsync(id)
                ? Result.Success()
                : Result.Failure($"Не удалось удалить входящий документ Ветис {id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка удаления входящего документа Ветис {id}: {ex.Message}");
        }
    }

    public async Task<Result<List<VetisIncomingDocumentEntity>>> List(VetisIncomingFilter filter, int skip, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisIncomingDocumentEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(VetisIncomingQuery.List(filter, skip, limit));
    }
}
