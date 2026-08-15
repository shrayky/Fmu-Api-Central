using CSharpFunctionalExtensions;
using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Dto.Responces;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.SoftwareUpdateFiles;

namespace Domain.Entitys.Instance.Interfaces;

public interface IInstanceManagerService
{
    Task<Result<FmuApiCentralResponse>> UpdateFmuApiInstanceInformation(string instanceData);
    Task<PaginatedResponse<InstanceMonitoringInformation>> InstancesList(int pageNumber, int pageSize, InstanceListFilter filter);
    Task<bool> CreateNew(InstanceMonitoringInformation instanceInformation);
    Task<bool> Delete(string instance);
    Task<string> InstanceSettings(string token);
    Task<Result> SettingsUploaded(string token);
    Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdate(string token, long? rangeFrom);
    Task<Result<List<InstanceMonitoringInformation>>> OfflineInstance(DateTime toDate);
    Task<Result<List<InstanceMonitoringInformation>>> All();
}