using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;

namespace Domain.Entitys.Interfaces;

public interface IInstanceRepository
{
    Task<Result> Update(InstanceEntity instance);
    Task<Result<InstanceEntity>> ByToken(string token);
    Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, InstanceListFilter filter);
    Task<Result<bool>> CreateInstance(InstanceEntity instance);
    Task<Result<bool>> DeleteInstance(InstanceEntity instance);
    Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate);
    Task<Result<List<InstanceEntity>>> All();
    Task<Result<List<InstanceEntity>>> ByGroupIds(IReadOnlyList<string> groupIds);
    Task<Result> ClearGroupLink(string groupId);
}