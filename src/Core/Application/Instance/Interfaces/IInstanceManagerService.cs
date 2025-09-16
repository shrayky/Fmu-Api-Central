using Application.Instance.DTO;
using CSharpFunctionalExtensions;
using Domain.Dto.fmuApiAnswer;
using Domain.Dto.Responces;

namespace Application.Instance.Interfaces;

public interface IInstanceManagerService
{
    Task<Result<FmuApiAnswer>> Update(string instanceData);
    Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(int pageNumber, int pageSize, string filter = "");
    Task<bool> CreateNew(InstanceMonitoringInformation instanceInformation);
    Task<bool> Delete(string instance);

}