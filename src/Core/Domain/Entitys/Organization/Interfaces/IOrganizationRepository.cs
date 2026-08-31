using CSharpFunctionalExtensions;
using Domain.Dto.Responces;

namespace Domain.Entitys.Organization.Interfaces;

public interface IOrganizationRepository
{
    Task<Result> Create(OrganizationEntity entity);
    Task<Result> Update(OrganizationEntity entity);
    Task<Result<OrganizationEntity>> GetById(string id);
    Task<Result<OrganizationEntity>> GetByInn(string inn);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize);
    Task<List<OrganizationEntity>> All();
}
