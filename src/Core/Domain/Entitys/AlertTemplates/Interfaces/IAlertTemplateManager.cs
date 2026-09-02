using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.AlertTemplates.Dto;

namespace Domain.Entitys.AlertTemplates.Interfaces;

public interface IAlertTemplateManager
{
    Task<Result> Create(AlertTemplateView data);
    Task<Result> Update(AlertTemplateView data);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<AlertTemplateView>> List(int pageNumber, int pageSize);
    Task<Result> EnsureDefaults();
}
