using CSharpFunctionalExtensions;
using Domain.Attributes;
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
        int limit)
    {
        var offset = VetisPaging.Skip(skip);
        var size = VetisPaging.Limit(limit);
        var filter = new VetisIncomingFilter
        {
            OrganizationId = organizationId ?? "",
            EnterpriseGuid = enterpriseGuid ?? "",
            Status = KnownStatuses.Contains(status) ? status : ""
        };

        var fetched = await _incoming.List(filter, offset, size + 1);
        if (fetched.IsFailure)
            return Result.Failure<VetisRowsPage<VetisIncomingRow>>(fetched.Error);

        var directory = new VetisDirectory(await _organizations.All());
        var rows = fetched.Value.Select(item => ToRow(item, directory)).ToList();
        return Result.Success(VetisPaging.Page(rows, offset, size));
    }

    public async Task<Result<VetisIncomingView>> GetAsync(string id)
    {
        var found = await _incoming.Find(id);
        if (found.IsFailure)
            return Result.Failure<VetisIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<VetisIncomingView>("Документ не найден.");

        var directory = new VetisDirectory(await _organizations.All());
        var document = found.Value;
        var view = new VetisIncomingView
        {
            Vsds = document.Vsds
                .Select(vsd => new VetisVsdView
                {
                    Uuid = vsd.Uuid,
                    IssueNumber = vsd.IssueNumber,
                    IssueDate = vsd.IssueDate,
                    Status = vsd.Status,
                    ProductName = vsd.ProductName,
                    Volume = vsd.Volume,
                    Transport = VetisTransport.Describe(vsd.RawXml),
                    ConsignorName = vsd.ConsignorName,
                    ReceivedAt = vsd.ReceivedAt,
                    RawXml = vsd.RawXml
                })
                .ToList()
        };
        Fill(view, document, directory);
        return Result.Success(view);
    }

    public async Task<List<VetisFilterOrganization>> FiltersAsync()
    {
        var organizations = await _organizations.All();
        return organizations
            .Where(item => item.VetisSettings.Enable)
            .Select(item => new VetisFilterOrganization
            {
                Id = item.Id,
                Name = item.Name,
                Enterprises = item.VetisSettings.Enterprises
                    .Select(enterprise => new VetisFilterEnterprise
                    {
                        Guid = enterprise.Guid,
                        Name = VetisDirectory.Caption(enterprise)
                    })
                    .ToList()
            })
            .ToList();
    }

    private static VetisIncomingRow ToRow(VetisIncomingDocumentEntity document, VetisDirectory directory)
    {
        var row = new VetisIncomingRow();
        Fill(row, document, directory);
        return row;
    }

    private static void Fill(VetisIncomingRow row, VetisIncomingDocumentEntity document, VetisDirectory directory)
    {
        row.Id = document.Id;
        row.Number = document.Number;
        row.Date = document.Date;
        row.Loose = document.Loose;
        row.Consignor = document.Consignor;
        row.OrganizationId = document.OrganizationId;
        row.OrganizationName = directory.OrganizationName(document.OrganizationId);
        row.EnterpriseGuid = document.EnterpriseGuid;
        row.EnterpriseName = directory.EnterpriseName(document.OrganizationId, document.EnterpriseGuid);
        row.Status = document.Status;
        row.ReceivedAt = document.ReceivedAt;
        row.VsdCount = document.VsdCount;
        row.AcceptedCount = document.AcceptedCount;
        row.SortAt = document.SortAt;
    }
}
