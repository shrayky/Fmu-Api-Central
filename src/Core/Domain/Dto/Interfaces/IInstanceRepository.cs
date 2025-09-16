using CSharpFunctionalExtensions;
using Domain.Dto.Responces;

namespace Domain.Dto.Interfaces;

public interface IInstanceRepository
{
    Task<Result> Update(InstanceEntity instance);
    Task<Result<InstanceEntity>> ByToken(string token);
    Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, string filter = "");
    Task<Result<bool>> CreateInstance(InstanceEntity instance);
    Task<Result<bool>> DeleteInstance(InstanceEntity instance);
}