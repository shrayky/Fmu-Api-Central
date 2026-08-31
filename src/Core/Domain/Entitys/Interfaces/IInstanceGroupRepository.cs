using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.InstanceGroup;

namespace Domain.Entitys.Interfaces;

public interface IInstanceGroupRepository
{
    Task<Result> Create(InstanceGroupEntity entity);
    Task<Result> Update(InstanceGroupEntity entity);
    Task<Result<InstanceGroupEntity>> GetById(string id);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize);
    Task<List<InstanceGroupEntity>> All();
    Task<List<InstanceGroupEntity>> ByListId(List<string> ids);
}
