using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Etrn;
using Domain.Entitys.Organization.Interfaces;

namespace Application.Tests.Fakes;

internal sealed class MemoryEtrnParameters : IParametersService
{
    public Parameters Value { get; } = new();

    public Task<Parameters> Current() => Task.FromResult(Value);

    public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
}

internal sealed class MemoryEtrnIncoming : IEtrnIncomingRepository
{
    public Dictionary<string, EtrnIncomingDocumentEntity> Store { get; } = new(StringComparer.Ordinal);

    public bool FailSave { get; set; }

    public string? FindError { get; set; }

    public int SaveCalls { get; private set; }

    public Task<Result<EtrnIncomingDocumentEntity?>> Find(string id)
    {
        if (FindError != null)
            return Task.FromResult(Result.Failure<EtrnIncomingDocumentEntity?>(FindError));

        return Task.FromResult(Result.Success(
            Store.TryGetValue(id, out var found) ? Clone(found) : null));
    }

    public Task<Result> Save(EtrnIncomingDocumentEntity document)
    {
        SaveCalls++;
        if (FailSave)
            return Task.FromResult(Result.Failure("БД недоступна"));

        Store[document.Id] = Clone(document);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<List<EtrnIncomingDocumentEntity>>> List(EtrnIncomingFilter filter, int skip, int limit)
    {
        var rows = Store.Values
            .Where(item => filter.OrganizationId.Length == 0 || item.OrganizationId == filter.OrganizationId)
            .Where(item => filter.ConnectionId.Length == 0 || item.Operator.ConnectionId == filter.ConnectionId)
            .Where(item => filter.Status.Length == 0 || item.Status == filter.Status)
            .OrderByDescending(item => item.SortAt)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Skip(skip)
            .Take(limit)
            .Select(Clone)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    public Task<Result<List<EtrnIncomingDocumentEntity>>> StaleSending(long beforeUnix, int limit)
    {
        var rows = Store.Values
            .Where(item => item.Status == EtrnDocumentStatus.Sending
                           && item.SendingAtUnix > 0
                           && item.SendingAtUnix < beforeUnix)
            .OrderBy(item => item.SendingAtUnix)
            .Take(limit)
            .Select(Clone)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    internal static EtrnIncomingDocumentEntity Clone(EtrnIncomingDocumentEntity entity) =>
        JsonSerializer.Deserialize<EtrnIncomingDocumentEntity>(JsonSerializer.Serialize(entity))!;
}

internal sealed class MemoryEtrnSyncStates : IEtrnSyncStateRepository
{
    public Dictionary<string, EtrnSyncState> Store { get; } = new(StringComparer.Ordinal);

    public Task<Result<EtrnSyncState?>> Find(string connectionId) =>
        Task.FromResult(Result.Success(
            Store.TryGetValue(connectionId, out var found) ? Clone(found) : null));

    public Task<Result> Save(EtrnSyncState state)
    {
        Store[state.ConnectionId] = Clone(state);
        return Task.FromResult(Result.Success());
    }

    private static EtrnSyncState Clone(EtrnSyncState state) =>
        JsonSerializer.Deserialize<EtrnSyncState>(JsonSerializer.Serialize(state))!;
}

internal sealed class MemoryEtrnRequests : IEtrnRequestRepository
{
    public Dictionary<string, EtrnRequestEntity> Store { get; } = new(StringComparer.Ordinal);

    public Task<Result> Save(EtrnRequestEntity request)
    {
        Store[request.Id] = request;
        return Task.FromResult(Result.Success());
    }

    public Task<Result<EtrnRequestEntity?>> Find(string id) =>
        Task.FromResult(Result.Success(Store.TryGetValue(id, out var found) ? found : null));

    public Task<Result<List<EtrnRequestEntity>>> List(EtrnRequestFilter filter, int skip, int limit)
    {
        var rows = Store.Values
            .Where(item => filter.OrganizationId.Length == 0 || item.OrganizationId == filter.OrganizationId)
            .Where(item => filter.ConnectionId.Length == 0 || item.Operator.ConnectionId == filter.ConnectionId)
            .OrderByDescending(item => item.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    public Task<Result<List<EtrnRequestEntity>>> ListExpired(bool completed, long createdBefore, int limit)
    {
        var rows = Store.Values
            .Where(item => item.Completed == completed && item.CreatedAt < createdBefore)
            .OrderBy(item => item.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    public Task<Result> Delete(IReadOnlyCollection<string> ids)
    {
        foreach (var id in ids)
            Store.Remove(id);

        return Task.FromResult(Result.Success());
    }
}

internal sealed class ScriptedEtrnOperator : IEtrnOperator
{
    private readonly Queue<EtrnFetchResult> _fetch = new();
    private readonly Queue<EtrnExecuteResult> _execute = new();
    private readonly Queue<EtrnStageResult> _stage = new();

    public string Type => EtrnOperatorType.Saby;

    public int FetchCalls { get; private set; }

    public int ExecuteCalls { get; private set; }

    public EtrnOperatorContext? LastContext { get; private set; }

    public EtrnExecuteCommand? LastCommand { get; private set; }

    public string? StatusAtExecute { get; private set; }

    public Exception? ThrowFromFetch { get; set; }

    public Exception? ThrowFromExecute { get; set; }

    public bool HoldExecute { get; set; }

    public TaskCompletionSource ExecuteEntered { get; } = new();

    public TaskCompletionSource ExecuteRelease { get; } = new();

    public void EnqueueFetch(EtrnFetchResult result) => _fetch.Enqueue(result);

    public void EnqueueExecute(EtrnExecuteResult result) => _execute.Enqueue(result);

    public void EnqueueStage(EtrnStageResult result) => _stage.Enqueue(result);

    public Task<Result<EtrnFetchResult>> FetchAsync(EtrnOperatorContext context, CancellationToken cancellationToken)
    {
        FetchCalls++;
        LastContext = context;
        if (ThrowFromFetch != null)
            throw ThrowFromFetch;

        var result = _fetch.Count > 0
            ? _fetch.Dequeue()
            : new EtrnFetchResult { Completed = true, Position = EtrnPollPosition.Empty };
        return Task.FromResult(Result.Success(result));
    }

    public async Task<Result<EtrnExecuteResult>> ExecuteAsync(
        EtrnOperatorContext context,
        EtrnExecuteCommand command,
        CancellationToken cancellationToken)
    {
        ExecuteCalls++;
        LastContext = context;
        LastCommand = command;
        StatusAtExecute = command.Document.Status;
        if (ThrowFromExecute != null)
            throw ThrowFromExecute;

        if (HoldExecute)
        {
            ExecuteEntered.TrySetResult();
            await ExecuteRelease.Task;
        }

        var result = _execute.Count > 0
            ? _execute.Dequeue()
            : new EtrnExecuteResult { Completed = true };
        return Result.Success(result);
    }

    public Task<Result<EtrnStageResult>> ReadStageAsync(
        EtrnOperatorContext context,
        string operatorDocumentId,
        CancellationToken cancellationToken)
    {
        LastContext = context;
        var result = _stage.Count > 0
            ? _stage.Dequeue()
            : new EtrnStageResult { Completed = true, Stage = EtrnPollEvent.AcceptanceStage };
        return Task.FromResult(Result.Success(result));
    }

    public static EtrnFetchResult Fetching(params EtrnFetchedDocument[] documents) => new()
    {
        Completed = true,
        Position = new EtrnPollPosition("e-last", "d-last"),
        Documents = documents.ToList(),
        Exchanges =
        [
            new EtrnExchange
            {
                Method = EtrnMethods.ChangesList,
                Parameters = "страница изменений",
                RequestBody = "{}",
                ResponseBody = "{}",
                Completed = true
            }
        ]
    };

    public static EtrnFetchedDocument Document(
        string id,
        string number = "ТН-1",
        string date = "2026-11-04",
        string consignor = "Поставщик",
        string address = "Москва, ул. Ленина, 1",
        string uid = "uid-1") => new()
    {
        OperatorDocumentId = id,
        Number = number,
        Date = date,
        Consignor = consignor,
        DeliveryAddress = address,
        Uid = uid,
        RawXml = "<Файл/>",
        Lines =
        [
            new EtrnCargoLine { Index = 1, Name = "Ящики", Places = 10, GrossWeight = "100" }
        ]
    };
}
