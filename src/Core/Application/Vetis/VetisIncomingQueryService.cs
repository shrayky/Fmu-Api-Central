using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingQueryService
{
    private static readonly string[] KnownStatuses =
    [
        VetisIncomingStatus.Accepted,
        VetisIncomingStatus.Partial,
        VetisIncomingStatus.NotAccepted,
        VetisIncomingStatus.Rejected
    ];

    private readonly IVetisIncomingRepository _incoming;
    private readonly IOrganizationRepository _organizations;

    public VetisIncomingQueryService(IVetisIncomingRepository incoming, IOrganizationRepository organizations)
    {
        _incoming = incoming;
        _organizations = organizations;
    }

    public async Task<Result<VetisRowsPage<VetisIncomingRow>>> ListAsync(
        string organizationId,
        string enterpriseGuid,
        string status,
        int skip,
        int limit,
        string vsdUuid = "",
        string salePoint = "")
    {
        var offset = VetisPaging.Skip(skip);
        var size = VetisPaging.Limit(limit);
        var uuid = NormalizeVsdUuid(vsdUuid);
        var (salePointUid, onlyWithout) = SalePointFilter.Parse(salePoint);
        if ((salePointUid.Length > 0 || onlyWithout) && string.IsNullOrWhiteSpace(organizationId))
            return Result.Failure<VetisRowsPage<VetisIncomingRow>>("Выберите организацию для отбора по торговой точке");

        // GUID ищется по всем статусам: документ уже может быть погашен, а журнал открыт на «Не принят».
        // Точка при этом остаётся: иначе отбор в журнале молча снимается.
        var filter = uuid.Length > 0
            ? new VetisIncomingFilter
            {
                VsdUuid = uuid,
                OrganizationId = salePointUid.Length > 0 || onlyWithout ? organizationId ?? "" : "",
                SalePointUid = salePointUid,
                OnlyWithoutSalePoint = onlyWithout
            }
            : new VetisIncomingFilter
            {
                OrganizationId = organizationId ?? "",
                EnterpriseGuid = enterpriseGuid ?? "",
                Status = KnownStatuses.Contains(status) ? status : "",
                SalePointUid = salePointUid,
                OnlyWithoutSalePoint = onlyWithout
            };

        var fetched = await _incoming.List(filter, offset, size + 1);
        if (fetched.IsFailure)
            return Result.Failure<VetisRowsPage<VetisIncomingRow>>(fetched.Error);

        var organizations = await _organizations.All();
        var directory = new VetisDirectory(organizations);
        var salePoints = new SalePointDirectory(organizations);
        var rows = fetched.Value.Select(item => ToRow(item, directory, salePoints)).ToList();
        return Result.Success(VetisPaging.Page(rows, offset, size));
    }

    public async Task<Result<VetisIncomingView>> GetAsync(string id)
    {
        var found = await _incoming.Find(id);
        if (found.IsFailure)
            return Result.Failure<VetisIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<VetisIncomingView>("Документ не найден.");

        var organizations = await _organizations.All();
        var directory = new VetisDirectory(organizations);
        var salePoints = new SalePointDirectory(organizations);
        var document = found.Value;
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        var earliest = VetisExpiry.Earliest(document.Vsds.Select(vsd => vsd.RawXml));
        var view = new VetisIncomingView
        {
            SalePoints = salePoints.Options(document.OrganizationId, openOnly: true).ToList(),
            Vsds = document.Vsds
                .Select(vsd =>
                {
                    var expiry = VetisExpiry.Text(vsd.RawXml);
                    return new VetisVsdView
                    {
                        Uuid = vsd.Uuid,
                        IssueNumber = vsd.IssueNumber,
                        IssueDate = vsd.IssueDate,
                        Status = vsd.Status,
                        ProductName = vsd.ProductName,
                        Volume = vsd.Volume,
                        ActualVolume = vsd.ActualVolume,
                        Transport = VetisTransport.Describe(vsd.RawXml),
                        ConsignorName = vsd.ConsignorName,
                        ReceivedAt = vsd.ReceivedAt,
                        LastError = vsd.LastError,
                        RawXml = vsd.RawXml,
                        ExpiryDate = expiry,
                        ExpiryExpired = VetisExpiry.Expired(expiry, today)
                    };
                })
                .ToList(),
            ExpiryDate = earliest,
            ExpiryExpired = VetisExpiry.Expired(earliest, today)
        };
        Fill(view, document, directory, salePoints);
        return Result.Success(view);
    }

    public Task<List<VetisFilterOrganization>> FiltersAsync() =>
        DirectoryAsync(new Dictionary<(string OrganizationId, string Guid), int>());

    /// <summary>
    /// Отдельный обход непринятых: журнал открывается по списку площадок, числа дописываются следом.
    /// </summary>
    public async Task<Result<List<VetisFilterOrganization>>> NotAcceptedCountsAsync()
    {
        var keys = await _incoming.NotAcceptedKeys();
        if (keys.IsFailure)
            return Result.Failure<List<VetisFilterOrganization>>(keys.Error);

        var counts = keys.Value
            .GroupBy(item => (item.OrganizationId, Guid: item.EnterpriseGuid), KeyComparer.Instance)
            .ToDictionary(group => group.Key, group => group.Count(), KeyComparer.Instance);

        return Result.Success(await DirectoryAsync(counts));
    }

    private async Task<List<VetisFilterOrganization>> DirectoryAsync(
        IReadOnlyDictionary<(string OrganizationId, string Guid), int> counts)
    {
        var organizations = await _organizations.All();
        return organizations
            .Where(item => item.VetisSettings.Enable)
            .Select(item =>
            {
                var enterprises = item.VetisSettings.Enterprises
                    .Select(enterprise => new VetisFilterEnterprise
                    {
                        Guid = enterprise.Guid,
                        Name = VetisDirectory.Caption(enterprise),
                        NotAcceptedCount = counts.GetValueOrDefault((item.Id, enterprise.Guid))
                    })
                    .ToList();
                return new VetisFilterOrganization
                {
                    Id = item.Id,
                    Name = item.Name,
                    NotAcceptedCount = enterprises.Sum(enterprise => enterprise.NotAcceptedCount),
                    Enterprises = enterprises,
                    SalePoints = (item.SalePoints ?? [])
                        .Select(point => new SalePointOption { Uid = point.Uid, Name = point.Name, Closed = point.Closed })
                        .ToList()
                };
            })
            .ToList();
    }

    private sealed class KeyComparer : IEqualityComparer<(string OrganizationId, string Guid)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((string OrganizationId, string Guid) left, (string OrganizationId, string Guid) right) =>
            string.Equals(left.OrganizationId, right.OrganizationId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Guid, right.Guid, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string OrganizationId, string Guid) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.OrganizationId ?? ""),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Guid ?? ""));
    }

    private static string NormalizeVsdUuid(string value)
    {
        var text = (value ?? "").Trim().Trim('{', '}').ToLowerInvariant();
        if (text.Length == 32 && text.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            text = $"{text[..8]}-{text[8..12]}-{text[12..16]}-{text[16..20]}-{text[20..]}";

        return text;
    }

    private static VetisIncomingRow ToRow(
        VetisIncomingDocumentEntity document,
        VetisDirectory directory,
        SalePointDirectory salePoints)
    {
        var row = new VetisIncomingRow();
        Fill(row, document, directory, salePoints);
        return row;
    }

    private static void Fill(
        VetisIncomingRow row,
        VetisIncomingDocumentEntity document,
        VetisDirectory directory,
        SalePointDirectory salePoints)
    {
        row.Id = document.Id;
        row.Number = document.Number;
        row.Date = document.Date;
        row.Loose = document.Loose;
        row.Consignor = document.Consignor;
        row.SalePointUid = document.SalePointUid;
        row.SalePointName = salePoints.Name(document.OrganizationId, document.SalePointUid);
        row.SalePointClosed = salePoints.Closed(document.OrganizationId, document.SalePointUid);
        row.OrganizationId = document.OrganizationId;
        row.OrganizationName = directory.OrganizationName(document.OrganizationId);
        row.EnterpriseGuid = document.EnterpriseGuid;
        row.EnterpriseName = directory.EnterpriseName(document.OrganizationId, document.EnterpriseGuid);
        row.Status = document.Status;
        row.ReceivedAt = document.ReceivedAt;
        row.QuenchedBy = document.QuenchedBy;
        row.QuenchedAt = document.QuenchedAt;
        row.VsdCount = document.VsdCount;
        row.AcceptedCount = document.AcceptedCount;
        row.SortAt = document.SortAt;
    }
}
