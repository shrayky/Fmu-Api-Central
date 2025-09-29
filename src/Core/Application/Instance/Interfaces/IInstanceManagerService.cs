using Application.Instance.DTO;
using CSharpFunctionalExtensions;
using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Dto.Responces;

namespace Application.Instance.Interfaces;

public interface IInstanceManagerService
{
    Task<Result<FmuApiCentralResponse>> UpdateFmuApiInstanceInformation(string instanceData);
    Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(int pageNumber, int pageSize, string filter = "");
    Task<bool> CreateNew(InstanceMonitoringInformation instanceInformation);
    Task<bool> Delete(string instance);
    Task<string> InstanceSettings(string token);
    Task<Result> SettingsUploaded(string token);
    Task<Result<Stream>> FmuApiUpdate(string token);
}