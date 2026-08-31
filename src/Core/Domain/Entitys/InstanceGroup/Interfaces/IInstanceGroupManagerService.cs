using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.InstanceGroup.Dto;

namespace Domain.Entitys.InstanceGroup.Interfaces;

public interface IInstanceGroupManagerService
{
    Task<Result> Create(InstanceGroupView data);
    Task<Result> Update(InstanceGroupView data);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<InstanceGroupView>> List(int pageNumber, int pageSize);
    Task<List<GroupLink>> AllLinks();
    Task<Result<ForceUpdateResult>> AssignForcedUpdate(IReadOnlyList<string> groupIds, string updateId);
    Task<Result<ForceUpdateResult>> ExportSettings(IReadOnlyList<string> groupIds);
}
