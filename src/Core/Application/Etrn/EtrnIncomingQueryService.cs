using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Etrn;

internal static class EtrnPaging
{
    private const int DefaultLimit = 50;

    private const int MaxLimit = 200;

    public static int Limit(int requested) =>
        requested <= 0 ? DefaultLimit : Math.Min(requested, MaxLimit);

    public static int Skip(int requested) => Math.Max(0, requested);

    public static EtrnRowsPage<T> Page<T>(List<T> fetched, int skip, int limit) =>
        new()
        {
            Rows = fetched.Take(limit).ToList(),
            Skip = skip,
            HasMore = fetched.Count > limit
        };
}

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnIncomingQueryService
{
    private readonly IEtrnIncomingRepository _incoming;
    private readonly IOrganizationRepository _organizations;
    private readonly IEtrnSyncStateRepository _syncStates;

    public EtrnIncomingQueryService(
        IEtrnIncomingRepository incoming,
        IOrganizationRepository organizations,
        IEtrnSyncStateRepository syncStates)
    {
        _incoming = incoming;
        _organizations = organizations;
        _syncStates = syncStates;
    }

    public async Task<Result<EtrnRowsPage<EtrnIncomingRow>>> ListAsync(
        string organizationId,
        string connectionId,
        string status,
        int skip,
        int limit)
    {
        var offset = EtrnPaging.Skip(skip);
        var size = EtrnPaging.Limit(limit);
        var filter = new EtrnIncomingFilter
        {
            OrganizationId = organizationId ?? "",
            ConnectionId = connectionId ?? "",
            Status = status ?? ""
        };

        var fetched = await _incoming.List(filter, offset, size + 1);
        if (fetched.IsFailure)
            return Result.Failure<EtrnRowsPage<EtrnIncomingRow>>(fetched.Error);

        var organizations = await _organizations.All();
        var rows = fetched.Value.Select(item => ToRow(item, organizations)).ToList();
        return Result.Success(EtrnPaging.Page(rows, offset, size));
    }

    public async Task<Result<EtrnIncomingView>> GetAsync(string id)
    {
        var found = await _incoming.Find(id);
        if (found.IsFailure)
            return Result.Failure<EtrnIncomingView>(found.Error);
        if (found.Value == null)
            return Result.Failure<EtrnIncomingView>("Документ не найден.");

        var organizations = await _organizations.All();
        var document = found.Value;
        var view = new EtrnIncomingView
        {
            Uid = document.Uid,
            GeneralComment = document.GeneralComment,
            SignedAt = document.SignedAt,
            Lines = document.Lines
                .Select(line => new EtrnLineView
                {
                    Index = line.Index,
                    Name = line.Name,
                    Places = line.Places,
                    GrossWeight = line.GrossWeight,
                    FactPlaces = line.FactPlaces,
                    FactWeight = line.FactWeight,
                    Comment = line.Comment,
                    EditablePlaces = line.HasPlaces,
                    EditableWeight = line.HasWeight
                })
                .ToList(),
            CanDecide = document.CanDecide
        };
        Fill(view, document, organizations);
        return Result.Success(view);
    }

    public async Task<List<EtrnFilterOrganization>> FiltersAsync()
    {
        var organizations = await _organizations.All();
        var result = new List<EtrnFilterOrganization>();
        foreach (var item in organizations.Where(item => (item.EtrnConnections ?? []).Count > 0))
        {
            var organization = new EtrnFilterOrganization { Id = item.Id, Name = item.Name };
            foreach (var connection in item.EtrnConnections ?? [])
            {
                var sync = await _syncStates.Find(connection.Id);
                organization.Connections.Add(new EtrnFilterConnection
                {
                    Id = connection.Id,
                    Name = string.IsNullOrWhiteSpace(connection.Name)
                        ? EtrnOperatorType.Label(connection.Type)
                        : connection.Name.Trim(),
                    Type = connection.Type,
                    Enable = connection.Enable,
                    LastError = sync.IsSuccess ? sync.Value?.LastError ?? "" : sync.Error
                });
            }

            result.Add(organization);
        }

        return result;
    }

    private static EtrnIncomingRow ToRow(EtrnIncomingDocumentEntity document, List<OrganizationEntity> organizations)
    {
        var row = new EtrnIncomingRow();
        Fill(row, document, organizations);
        return row;
    }

    private static void Fill(
        EtrnIncomingRow row,
        EtrnIncomingDocumentEntity document,
        List<OrganizationEntity> organizations)
    {
        row.Id = document.Id;
        row.Number = document.Number;
        row.Date = document.Date;
        row.Consignor = document.Consignor;
        row.DeliveryAddress = document.DeliveryAddress;
        row.OrganizationId = document.OrganizationId;
        row.OrganizationName = organizations
            .FirstOrDefault(item => string.Equals(item.Id, document.OrganizationId, StringComparison.OrdinalIgnoreCase))?.Name ?? "";
        row.ConnectionId = document.Operator.ConnectionId;
        row.OperatorName = document.Operator.DisplayName;
        row.Status = document.Status;
        row.StatusLabel = document.StatusLabel;
        row.Action = document.Action;
        row.LastError = document.LastError;
        row.SortAt = document.SortAt;
    }
}

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnRequestQueryService
{
    private readonly IEtrnRequestRepository _requests;
    private readonly IOrganizationRepository _organizations;

    public EtrnRequestQueryService(IEtrnRequestRepository requests, IOrganizationRepository organizations)
    {
        _requests = requests;
        _organizations = organizations;
    }

    public async Task<Result<EtrnRowsPage<EtrnRequestRow>>> ListAsync(
        string organizationId,
        string connectionId,
        int skip,
        int limit)
    {
        var offset = EtrnPaging.Skip(skip);
        var size = EtrnPaging.Limit(limit);
        var fetched = await _requests.List(
            new EtrnRequestFilter
            {
                OrganizationId = organizationId ?? "",
                ConnectionId = connectionId ?? ""
            },
            offset,
            size + 1);
        if (fetched.IsFailure)
            return Result.Failure<EtrnRowsPage<EtrnRequestRow>>(fetched.Error);

        var organizations = await _organizations.All();
        var rows = fetched.Value.Select(item => ToRow(item, organizations)).ToList();
        return Result.Success(EtrnPaging.Page(rows, offset, size));
    }

    public async Task<Result<EtrnRequestView>> GetAsync(string id)
    {
        var found = await _requests.Find(id);
        if (found.IsFailure)
            return Result.Failure<EtrnRequestView>(found.Error);
        if (found.Value == null)
            return Result.Failure<EtrnRequestView>("Запрос не найден.");

        var organizations = await _organizations.All();
        var view = new EtrnRequestView
        {
            RequestBody = found.Value.RequestBody,
            ResponseBody = found.Value.ResponseBody
        };
        Fill(view, found.Value, organizations);
        return Result.Success(view);
    }

    private static EtrnRequestRow ToRow(EtrnRequestEntity request, List<OrganizationEntity> organizations)
    {
        var row = new EtrnRequestRow();
        Fill(row, request, organizations);
        return row;
    }

    private static void Fill(
        EtrnRequestRow row,
        EtrnRequestEntity request,
        List<OrganizationEntity> organizations)
    {
        row.Id = request.Id;
        row.Method = request.Method;
        row.Parameters = request.Parameters;
        row.OrganizationId = request.OrganizationId;
        row.OrganizationName = organizations
            .FirstOrDefault(item => string.Equals(item.Id, request.OrganizationId, StringComparison.OrdinalIgnoreCase))?.Name ?? "";
        row.ConnectionId = request.Operator.ConnectionId;
        row.OperatorName = request.Operator.DisplayName;
        row.Completed = request.Completed;
        row.Error = request.Error;
        row.CreatedAt = request.CreatedAt;
    }
}
