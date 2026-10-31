using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using VetisApi.Xml;

namespace Application.Tests.Fakes;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

internal sealed class MemoryVetisParameters : IParametersService
{
    public Parameters Value { get; } = new();

    public MemoryVetisParameters(bool enable = true)
    {
        Value.VetisSettings.Enable = enable;
    }

    public Task<Parameters> Current() => Task.FromResult(Value);

    public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
}

/// <summary>
/// Отдаёт копии, как база: правка прочитанной карточки не меняет сохранённую.
/// </summary>
internal sealed class MemoryOrganizationRepository : IOrganizationRepository
{
    public Dictionary<string, OrganizationEntity> Store { get; } = new();

    public int Updates { get; private set; }

    public Task<Result> Create(OrganizationEntity entity)
    {
        Store[entity.Id] = Clone(entity);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> Update(OrganizationEntity entity)
    {
        Updates++;
        Store[entity.Id] = Clone(entity);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<OrganizationEntity>> GetById(string id) =>
        Task.FromResult(Store.TryGetValue(id, out var entity)
            ? Result.Success(Clone(entity))
            : Result.Failure<OrganizationEntity>("нет"));

    public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();

    public Task<Result> Delete(string id) => throw new NotImplementedException();

    public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize) => throw new NotImplementedException();

    public Task<List<OrganizationEntity>> All() => Task.FromResult(Store.Values.Select(Clone).ToList());

    private static OrganizationEntity Clone(OrganizationEntity entity) =>
        JsonSerializer.Deserialize<OrganizationEntity>(JsonSerializer.Serialize(entity))!;
}

internal sealed class MemoryVetisIncoming : IVetisIncomingRepository
{
    public Dictionary<string, VetisIncomingDocumentEntity> Store { get; } = new();

    public bool FailSave { get; set; }

    public string? FindError { get; set; }

    public int ReleaseCalls { get; private set; }

    public int FindCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public Task<Result<VetisIncomingDocumentEntity?>> Find(string id)
    {
        FindCalls++;
        return Task.FromResult(FindError == null
            ? Result.Success(Store.TryGetValue(id, out var found) ? Clone(found) : null)
            : Result.Failure<VetisIncomingDocumentEntity?>(FindError));
    }

    public Task<Result> Save(VetisIncomingDocumentEntity document)
    {
        SaveCalls++;
        if (FailSave)
            return Task.FromResult(Result.Failure("БД недоступна"));

        Store[document.Id] = Clone(document);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<List<VetisIncomingHead>>> HeadsAfter(string afterId, int limit)
    {
        var rows = Store.Values
            .Select(item => new VetisIncomingHead(item.Id, item.Date, item.SortAt))
            .Where(head => string.CompareOrdinal(head.Id, afterId ?? "") > 0)
            .OrderBy(head => head.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    public Task<Result> Delete(string id)
    {
        Store.Remove(id);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> Release(string organizationId, string enterpriseGuid, IReadOnlyCollection<string> uuids, IReadOnlyCollection<string> keepDocumentIds)
    {
        ReleaseCalls++;
        var keep = new HashSet<string>(keepDocumentIds, StringComparer.OrdinalIgnoreCase);
        foreach (var document in Store.Values.ToList())
        {
            if (document.OrganizationId != organizationId || document.EnterpriseGuid != enterpriseGuid)
                continue;
            if (keep.Contains(document.Id))
                continue;
            if (!document.Drop(uuids))
                continue;
            if (document.Vsds.Count == 0)
                Store.Remove(document.Id);
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<List<VetisIncomingDocumentEntity>>> List(VetisIncomingFilter filter, int skip, int limit)
    {
        var rows = Store.Values
            .Where(item => filter.OrganizationId.Length == 0 || item.OrganizationId == filter.OrganizationId)
            .Where(item => filter.EnterpriseGuid.Length == 0 || item.EnterpriseGuid == filter.EnterpriseGuid)
            .Where(item => filter.Status.Length == 0 || item.Status == filter.Status)
            .OrderByDescending(item => item.SortAt)
            .Skip(skip)
            .Take(limit)
            .Select(Clone)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    public string? NotAcceptedError { get; set; }

    public Task<Result<List<VetisIncomingKey>>> NotAcceptedKeys() =>
        Task.FromResult(NotAcceptedError == null
            ? Result.Success(Store.Values
                .Where(item => item.Status == VetisIncomingStatus.NotAccepted)
                .Select(item => new VetisIncomingKey
                {
                    OrganizationId = item.OrganizationId,
                    EnterpriseGuid = item.EnterpriseGuid
                })
                .ToList())
            : Result.Failure<List<VetisIncomingKey>>(NotAcceptedError));

    private static VetisIncomingDocumentEntity Clone(VetisIncomingDocumentEntity entity) =>
        JsonSerializer.Deserialize<VetisIncomingDocumentEntity>(JsonSerializer.Serialize(entity))!;
}

internal sealed class MemoryVetisApplications : IVetisApplicationRepository
{
    public Dictionary<string, VetisApplicationEntity> Store { get; } = new();

    public Task<Result> Save(VetisApplicationEntity application)
    {
        Store[application.Id] = application;
        return Task.FromResult(Result.Success());
    }

    public Task<Result<VetisApplicationEntity?>> Find(string id) =>
        Task.FromResult(Result.Success(Store.TryGetValue(id, out var found) ? found : null));

    public Task<Result<List<VetisApplicationEntity>>> List(VetisApplicationFilter filter, int skip, int limit)
    {
        var rows = Store.Values
            .Where(item => filter.OrganizationId.Length == 0 || item.OrganizationId == filter.OrganizationId)
            .Where(item => filter.EnterpriseGuid.Length == 0 || item.EnterpriseGuid == filter.EnterpriseGuid)
            .OrderByDescending(item => item.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .ToList();
        return Task.FromResult(Result.Success(rows));
    }

    public Task<Result<List<VetisApplicationEntity>>> ListExpired(bool completed, long createdBefore, int limit)
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

internal sealed class ScriptedVetisGateway : IVetisGateway
{
    private readonly Queue<Func<VetisIncomingRequest, VetisIncomingResult>> _incoming = new();

    public List<VetisIncomingRequest> Requests { get; } = [];

    public int QuenchCalls { get; private set; }

    public int ThrowFromCall { get; set; }

    private readonly Queue<VetisExchange> _quench = new();

    public VetisCall? LastCall { get; private set; }

    public Action<VetisIncomingRequest>? OnIncoming { get; set; }

    public Action? OnQuench { get; set; }

    public bool Hold { get; set; }

    public TaskCompletionSource Entered { get; } = new();

    public TaskCompletionSource Release { get; } = new();

    public void Enqueue(VetisIncomingResult result) => _incoming.Enqueue(_ => result);

    public void EnqueueQuench(VetisExchange exchange) => _quench.Enqueue(exchange);

    public void Enqueue(Func<VetisIncomingRequest, VetisIncomingResult> result) => _incoming.Enqueue(result);

    public Task<VetisExchange> CheckConnectionAsync(VetisCall call, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<VetisEnterpriseList> ListEnterprisesAsync(VetisCall call, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<VetisIncomingResult> GetIncomingAsync(
        VetisCall call,
        VetisIncomingRequest request,
        CancellationToken cancellationToken)
    {
        LastCall = call;
        Requests.Add(request);
        OnIncoming?.Invoke(request);
        var next = _incoming.Count > 0
            ? _incoming.Dequeue()
            : _ => Page(0);
        return Task.FromResult(next(request));
    }

    public async Task<VetisQuenchResult> QuenchAsync(
        VetisCall call,
        string rawXml,
        VetisQuenchOrder order,
        string uuid,
        CancellationToken cancellationToken)
    {
        LastCall = call;
        QuenchCalls++;
        OnQuench?.Invoke();
        if (ThrowFromCall > 0 && QuenchCalls >= ThrowFromCall)
            throw new InvalidOperationException("обрыв");

        // Держим только первый вызов: иначе повтор без замка упрётся в Release и тест зависнет вместо падения.
        if (Hold)
        {
            Entered.TrySetResult();
            if (QuenchCalls == 1)
                await Release.Task;
        }

        var exchange = _quench.Count > 0
            ? _quench.Dequeue()
            : new VetisExchange { Completed = false, Error = "Нет сценария гашения.", Status = "FAILED" };
        var quenched = VetisResponseReader.ReadQuenched(exchange.ResponseXml, uuid);
        return new VetisQuenchResult
        {
            Exchange = exchange,
            Status = quenched?.Status ?? "",
            ReceivedAt = quenched?.ReceivedAt ?? ""
        };
    }

    public static VetisIncomingResult Page(int pageCount, params VetisIncomingVsd[] documents) =>
        new()
        {
            Exchange = new VetisExchange
            {
                Operation = "GetVetDocumentChangesList",
                LocalTransactionId = Guid.NewGuid().ToString(),
                Status = "COMPLETED",
                Completed = true,
                RequestXml = "<request/>",
                ResponseXml = "<response/>"
            },
            Documents = documents,
            PageCount = pageCount
        };

    public static VetisIncomingResult Failed(string error) =>
        new()
        {
            Exchange = new VetisExchange
            {
                Operation = "GetVetDocumentChangesList",
                LocalTransactionId = Guid.NewGuid().ToString(),
                Status = "REJECTED",
                Completed = false,
                Error = error,
                RequestXml = "<request/>",
                ResponseXml = "<fault/>"
            }
        };
}
