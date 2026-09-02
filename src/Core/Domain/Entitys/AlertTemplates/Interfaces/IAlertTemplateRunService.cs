using CSharpFunctionalExtensions;
using Domain.Entitys.AlertTemplates.Dto;

namespace Domain.Entitys.AlertTemplates.Interfaces;

public interface IAlertTemplateRunService
{
    Task<Result> RunDueTemplates(DateTime now);
    Task<Result<AlertDatasetResult>> Preview(string script);
}
