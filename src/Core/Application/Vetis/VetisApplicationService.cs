using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisApplicationService
{
    private readonly IVetisApplicationRepository _applications;
    private readonly IOrganizationRepository _organizations;

    public VetisApplicationService(IVetisApplicationRepository applications, IOrganizationRepository organizations)
    {
        _applications = applications;
        _organizations = organizations;
    }

    public async Task<Result<VetisRowsPage<VetisApplicationRow>>> ListAsync(
        string organizationId,
        string enterpriseGuid,
        int skip,
        int limit)
    {
        var offset = VetisPaging.Skip(skip);
        var size = VetisPaging.Limit(limit);
        var filter = new VetisApplicationFilter
        {
            OrganizationId = organizationId ?? "",
            EnterpriseGuid = enterpriseGuid ?? ""
        };

        var fetched = await _applications.List(filter, offset, size + 1);
        if (fetched.IsFailure)
            return Result.Failure<VetisRowsPage<VetisApplicationRow>>(fetched.Error);

        var directory = new VetisDirectory(await _organizations.All());
        var rows = fetched.Value.Select(item => ToRow(item, directory)).ToList();
        return Result.Success(VetisPaging.Page(rows, offset, size));
    }

    public async Task<Result<VetisApplicationView>> GetAsync(string id)
    {
        var found = await _applications.Find(id);
        if (found.IsFailure)
            return Result.Failure<VetisApplicationView>(found.Error);

        if (found.Value == null)
            return Result.Failure<VetisApplicationView>("Заявка не найдена.");

        var directory = new VetisDirectory(await _organizations.All());
        var view = new VetisApplicationView
        {
            RequestXml = VetisXmlFormat.Pretty(found.Value.RequestXml),
            ResponseXml = VetisXmlFormat.Pretty(found.Value.ResponseXml)
        };
        Fill(view, found.Value, directory);
        return Result.Success(view);
    }

    private static VetisApplicationRow ToRow(VetisApplicationEntity application, VetisDirectory directory)
    {
        var row = new VetisApplicationRow();
        Fill(row, application, directory);
        return row;
    }

    private static void Fill(VetisApplicationRow row, VetisApplicationEntity application, VetisDirectory directory)
    {
        row.Id = application.Id;
        row.CreatedAt = application.CreatedAt;
        row.Operation = application.Operation;
        row.ApplicationId = application.ApplicationId;
        row.Status = application.Status;
        row.Completed = application.Completed;
        row.HasResponse = application.HasResponse;
        row.Error = application.Error;
        row.Parameters = application.Parameters;
        row.OrganizationId = application.OrganizationId;
        row.OrganizationName = directory.OrganizationName(application.OrganizationId);
        row.EnterpriseGuid = application.EnterpriseGuid;
        row.EnterpriseName = directory.EnterpriseName(application.OrganizationId, application.EnterpriseGuid);
    }
}
