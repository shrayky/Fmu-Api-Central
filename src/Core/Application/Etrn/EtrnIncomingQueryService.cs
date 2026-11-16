using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Etrn;
using Domain.Etrn.Xml;
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
        string number,
        string consignor,
        int skip,
        int limit,
        string salePoint = "")
    {
        var offset = EtrnPaging.Skip(skip);
        var size = EtrnPaging.Limit(limit);
        var (salePointUid, onlyWithout) = SalePointFilter.Parse(salePoint);
        if ((salePointUid.Length > 0 || onlyWithout) && string.IsNullOrWhiteSpace(organizationId))
            return Result.Failure<EtrnRowsPage<EtrnIncomingRow>>("Выберите организацию для отбора по торговой точке");

        var filter = new EtrnIncomingFilter
        {
            OrganizationId = organizationId ?? "",
            ConnectionId = connectionId ?? "",
            Status = status ?? "",
            Number = number ?? "",
            Consignor = consignor ?? "",
            SalePointUid = salePointUid,
            OnlyWithoutSalePoint = onlyWithout
        };

        var fetched = await _incoming.List(filter, offset, size + 1);
        if (fetched.IsFailure)
            return Result.Failure<EtrnRowsPage<EtrnIncomingRow>>(fetched.Error);

        var organizations = await _organizations.All();
        var salePoints = new SalePointDirectory(organizations);
        var rows = new List<EtrnIncomingRow>();
        foreach (var item in fetched.Value)
        {
            await RememberAddress(item);
            rows.Add(ToRow(item, organizations, salePoints));
        }

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
        var salePoints = new SalePointDirectory(organizations);
        var document = found.Value;
        await RememberAddress(document);
        await RememberWeight(document);
        var view = new EtrnIncomingView
        {
            Uid = document.Uid,
            GeneralComment = document.GeneralComment,
            SignedAt = document.SignedAt,
            SalePoints = salePoints.Options(document.OrganizationId, openOnly: true).ToList(),
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
        Fill(view, document, organizations, salePoints);
        return Result.Success(view);
    }

    public async Task<List<EtrnFilterOrganization>> FiltersAsync()
    {
        var organizations = await _organizations.All();
        var result = new List<EtrnFilterOrganization>();
        foreach (var item in organizations.Where(item => (item.EtrnConnections ?? []).Count > 0))
        {
            var organization = new EtrnFilterOrganization { Id = item.Id, Name = item.Name };
            organization.SalePoints = (item.SalePoints ?? [])
                .Select(point => new SalePointOption { Uid = point.Uid, Name = point.Name, Closed = point.Closed })
                .ToList();
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

    /// <summary>
    /// Карточки, сохранённые до чтения АдресДостГр, держат пустой адрес. Титул уже в базе,
    /// список его не отдаёт — читаем документ целиком и записываем адрес, чтобы следующий список шёл без разбора.
    /// </summary>
    private async Task RememberAddress(EtrnIncomingDocumentEntity listed)
    {
        if (!string.IsNullOrWhiteSpace(listed.DeliveryAddress))
            return;

        var full = listed;
        if (string.IsNullOrWhiteSpace(listed.RawXml))
        {
            var found = await _incoming.Find(listed.Id);
            if (found.IsFailure || found.Value == null)
                return;

            full = found.Value;
            if (!string.IsNullOrWhiteSpace(full.DeliveryAddress))
            {
                listed.DeliveryAddress = full.DeliveryAddress;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(full.RawXml))
            return;

        var title = SenderTitleReader.Read(full.RawXml);
        if (title.IsFailure || string.IsNullOrWhiteSpace(title.Value.Address))
            return;

        full.DeliveryAddress = title.Value.Address;
        listed.DeliveryAddress = title.Value.Address;
        await _incoming.Save(full);
    }

    /// <summary>
    /// Карточки, сохранённые до чтения ПлМасГруз, держат пустую массу. Титул уже в базе.
    /// </summary>
    private async Task RememberWeight(EtrnIncomingDocumentEntity document)
    {
        if (document.Lines.All(line => line.HasWeight))
            return;

        if (string.IsNullOrWhiteSpace(document.RawXml))
            return;

        var title = SenderTitleReader.Read(document.RawXml);
        if (title.IsFailure)
            return;

        var changed = false;
        foreach (var line in document.Lines)
        {
            if (line.HasWeight)
                continue;

            var source = title.Value.Lines.FirstOrDefault(item => item.Index == line.Index);
            if (source is not { HasWeight: true })
                continue;

            line.GrossWeight = source.GrossWeight;
            changed = true;
        }

        if (changed)
            await _incoming.Save(document);
    }

    private static EtrnIncomingRow ToRow(
        EtrnIncomingDocumentEntity document,
        List<OrganizationEntity> organizations,
        SalePointDirectory salePoints)
    {
        var row = new EtrnIncomingRow();
        Fill(row, document, organizations, salePoints);
        return row;
    }

    private static void Fill(
        EtrnIncomingRow row,
        EtrnIncomingDocumentEntity document,
        List<OrganizationEntity> organizations,
        SalePointDirectory salePoints)
    {
        row.Id = document.Id;
        row.Number = document.Number;
        row.Date = document.Date;
        row.Consignor = document.Consignor;
        row.DeliveryAddress = document.DeliveryAddress;
        row.SalePointUid = document.SalePointUid;
        row.SalePointName = salePoints.Name(document.OrganizationId, document.SalePointUid);
        row.SalePointClosed = salePoints.Closed(document.OrganizationId, document.SalePointUid);
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
