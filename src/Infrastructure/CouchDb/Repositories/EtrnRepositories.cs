using CSharpFunctionalExtensions;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class EtrnIncomingRepository : BaseCouchDbRepository<EtrnIncomingDocumentEntity>, IEtrnIncomingRepository
{
    public EtrnIncomingRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().EtrnIncoming, services)
    {
    }

    public async Task<Result<EtrnIncomingDocumentEntity?>> Find(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<EtrnIncomingDocumentEntity?>(DatabaseUnavailable);

        try
        {
            return Result.Success(await GetByIdAsync(id));
        }
        catch (Exception ex)
        {
            return Result.Failure<EtrnIncomingDocumentEntity?>($"Ошибка чтения документа ЭТрН {id}: {ex.Message}");
        }
    }

    public async Task<Result> Save(EtrnIncomingDocumentEntity document)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateAsync(document)
                ? Result.Success()
                : Result.Failure($"Не удалось сохранить документ ЭТрН {document.Id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка сохранения документа ЭТрН {document.Id}: {ex.Message}");
        }
    }

    public async Task<Result<List<EtrnIncomingDocumentEntity>>> List(EtrnIncomingFilter filter, int skip, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<EtrnIncomingDocumentEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(EtrnIncomingQuery.List(filter, skip, limit));
    }

    public async Task<Result<List<EtrnIncomingDocumentEntity>>> StaleSending(long beforeUnix, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<EtrnIncomingDocumentEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(EtrnIncomingQuery.StaleSending(beforeUnix, limit));
    }
}

public class EtrnSyncStateRepository : BaseCouchDbRepository<EtrnSyncState>, IEtrnSyncStateRepository
{
    public EtrnSyncStateRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().EtrnSyncStates, services)
    {
    }

    public async Task<Result<EtrnSyncState?>> Find(string connectionId)
    {
        if (!_appState.DbState())
            return Result.Failure<EtrnSyncState?>(DatabaseUnavailable);

        try
        {
            return Result.Success(await GetByIdAsync(EtrnSyncState.BuildId(connectionId)));
        }
        catch (Exception ex)
        {
            return Result.Failure<EtrnSyncState?>($"Ошибка чтения состояния опроса ЭТрН {connectionId}: {ex.Message}");
        }
    }

    public async Task<Result> Save(EtrnSyncState state)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            state.Id = EtrnSyncState.BuildId(state.ConnectionId);
            return await CreateAsync(state)
                ? Result.Success()
                : Result.Failure($"Не удалось сохранить состояние опроса ЭТрН {state.ConnectionId}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка сохранения состояния опроса ЭТрН {state.ConnectionId}: {ex.Message}");
        }
    }
}

public class EtrnRequestRepository : BaseCouchDbRepository<EtrnRequestEntity>, IEtrnRequestRepository
{
    public EtrnRequestRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().EtrnRequests, services)
    {
    }

    public async Task<Result> Save(EtrnRequestEntity request)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateAsync(request)
                ? Result.Success()
                : Result.Failure($"Не удалось сохранить запрос ЭТрН {request.Id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка сохранения запроса ЭТрН {request.Id}: {ex.Message}");
        }
    }

    public async Task<Result<EtrnRequestEntity?>> Find(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<EtrnRequestEntity?>(DatabaseUnavailable);

        try
        {
            return Result.Success(await GetByIdAsync(id));
        }
        catch (Exception ex)
        {
            return Result.Failure<EtrnRequestEntity?>($"Ошибка чтения запроса ЭТрН {id}: {ex.Message}");
        }
    }

    public async Task<Result<List<EtrnRequestEntity>>> List(EtrnRequestFilter filter, int skip, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<EtrnRequestEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(EtrnRequestQuery.List(filter, skip, limit));
    }

    public async Task<Result<List<EtrnRequestEntity>>> ListExpired(bool completed, long createdBefore, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<EtrnRequestEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(EtrnRequestQuery.Expired(completed, createdBefore, limit));
    }

    public async Task<Result> Delete(IReadOnlyCollection<string> ids)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            foreach (var id in ids)
                await DeleteAsync(id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка удаления запросов ЭТрН: {ex.Message}");
        }
    }
}
