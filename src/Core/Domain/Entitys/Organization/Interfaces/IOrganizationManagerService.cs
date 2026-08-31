using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Organization.Dto;
using Domain.TrueApiIntegration.Dto;

namespace Domain.Entitys.Organization.Interfaces;

public interface IOrganizationManagerService
{
    Task<Result> Create(OrganizationView data);
    Task<Result> Update(OrganizationView data);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<OrganizationView>> List(int pageNumber, int pageSize);
    Task<Result<TrueApiTokenView>> Token(string inn);
}
