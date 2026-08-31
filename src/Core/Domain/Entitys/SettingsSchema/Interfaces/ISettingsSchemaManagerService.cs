using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.SettingsSchema.Dto;

namespace Domain.Entitys.SettingsSchema.Interfaces;

public interface ISettingsSchemaManagerService
{
    Task<Result> Create(SettingsSchemaView data);
    Task<Result> Update(SettingsSchemaView data);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<SettingsSchemaView>> List(int pageNumber, int pageSize);
    Task<List<SettingsSchemaLink>> AllLinks();
    SettingsSchemaView Defaults();
}
