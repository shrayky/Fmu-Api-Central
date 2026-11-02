using CSharpFunctionalExtensions;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class VetisIncomingRepository : BaseCouchDbRepository<VetisIncomingDocumentEntity>, IVetisIncomingRepository
{
    private const int NotAcceptedPageSize = 2000;

    // Предохранитель от бесконечного листания, если CouchDB всегда отдаёт полную страницу.
    private const int MaxNotAcceptedPages = 500;

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

    public async Task<Result> Delete(string id)
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

    public async Task<Result<List<VetisIncomingHead>>> HeadsAfter(string afterId, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisIncomingHead>>(DatabaseUnavailable);

        var loaded = await ExecuteMangoQueryAsync(VetisIncomingQuery.HeadsAfter(afterId, limit));
        return loaded.IsFailure
            ? Result.Failure<List<VetisIncomingHead>>(loaded.Error)
            : Result.Success(loaded.Value
                .Select(document => new VetisIncomingHead(document.Id, document.Date, document.SortAt))
                .ToList());
    }

    public async Task<Result<List<VetisIncomingHead>>> OldestAfter(
        string organizationId,
        string status,
        bool dated,
        long afterSortAt,
        string afterId,
        int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisIncomingHead>>(DatabaseUnavailable);

        var loaded = await ExecuteMangoQueryAsync(
            VetisIncomingQuery.Oldest(organizationId, status, dated, afterSortAt, afterId, limit));
        return loaded.IsFailure
            ? Result.Failure<List<VetisIncomingHead>>(loaded.Error)
            : Result.Success(loaded.Value
                .Select(document => new VetisIncomingHead(document.Id, document.Date, document.SortAt))
                .ToList());
    }

    public async Task<Result<List<VetisIncomingKey>>> NotAcceptedKeys()
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisIncomingKey>>(DatabaseUnavailable);

        try
        {
            var keys = new List<VetisIncomingKey>();
            var afterId = "";
            for (var page = 0; page < MaxNotAcceptedPages; page++)
            {
                var loaded = await ExecuteMangoQueryAsync(VetisIncomingQuery.NotAcceptedAfter(afterId, NotAcceptedPageSize));
                if (loaded.IsFailure)
                    return Result.Failure<List<VetisIncomingKey>>(loaded.Error);

                keys.AddRange(loaded.Value.Select(document => new VetisIncomingKey
                {
                    OrganizationId = document.OrganizationId,
                    EnterpriseGuid = document.EnterpriseGuid
                }));
                if (loaded.Value.Count < NotAcceptedPageSize)
                    return Result.Success(keys);

                afterId = loaded.Value[^1].Id;
            }

            return Result.Failure<List<VetisIncomingKey>>($"Страниц больше {MaxNotAcceptedPages}.");
        }
        catch (Exception ex)
        {
            return Result.Failure<List<VetisIncomingKey>>($"Ошибка подсчёта непринятых документов Ветис: {ex.Message}");
        }
    }

    public async Task<Result<List<VetisOpenKey>>> OpenKeys()
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisOpenKey>>(DatabaseUnavailable);

        try
        {
            var keys = new List<VetisOpenKey>();
            foreach (var status in new[] { VetisIncomingStatus.NotAccepted, VetisIncomingStatus.Partial })
            {
                var page = await ReadOpenPage(status);
                if (page.IsFailure)
                    return Result.Failure<List<VetisOpenKey>>(page.Error);

                keys.AddRange(page.Value);
            }

            return Result.Success(keys);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<VetisOpenKey>>($"Ошибка подсчёта непогашенных документов Ветис: {ex.Message}");
        }
    }

    private async Task<Result<List<VetisOpenKey>>> ReadOpenPage(string status)
    {
        var keys = new List<VetisOpenKey>();
        var afterId = "";
        for (var page = 0; page < MaxNotAcceptedPages; page++)
        {
            var loaded = await ExecuteMangoQueryAsync(VetisIncomingQuery.StatusAfter(status, afterId, NotAcceptedPageSize));
            if (loaded.IsFailure)
                return Result.Failure<List<VetisOpenKey>>(loaded.Error);

            keys.AddRange(loaded.Value.Select(document => new VetisOpenKey(document.OrganizationId, status)));
            if (loaded.Value.Count < NotAcceptedPageSize)
                return Result.Success(keys);

            afterId = loaded.Value[^1].Id;
        }

        return Result.Failure<List<VetisOpenKey>>($"Страниц больше {MaxNotAcceptedPages}.");
    }
}
