using CSharpFunctionalExtensions;
using Domain.Dto.Responces;

namespace Domain.Entitys.AlertTemplates.Interfaces;

public interface IAlertTemplateRepository
{
    Task<Result> Create(AlertTemplateEntity entity);
    Task<Result> Update(AlertTemplateEntity entity);
    Task<Result> Delete(string id);
    Task<Result<AlertTemplateEntity>> GetById(string id);
    Task<PaginatedResponse<AlertTemplateEntity>> List(int pageNumber, int pageSize);
    Task<List<AlertTemplateEntity>> All();
    Task<List<AlertTemplateEntity>> AllEnabled();
}
