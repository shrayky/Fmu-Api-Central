using CSharpFunctionalExtensions;
using Domain.Dto.Responces;

namespace Domain.Entitys.SettingsSchema.Interfaces;

public interface ISettingsSchemaRepository
{
    Task<Result> Create(SettingsSchemaEntity entity);
    Task<Result> Update(SettingsSchemaEntity entity);
    Task<Result<SettingsSchemaEntity>> GetById(string id);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<SettingsSchemaEntity>> List(int pageNumber, int pageSize);
    Task<List<SettingsSchemaEntity>> All();
    Task<List<SettingsSchemaEntity>> ByListId(List<string> ids);
}
