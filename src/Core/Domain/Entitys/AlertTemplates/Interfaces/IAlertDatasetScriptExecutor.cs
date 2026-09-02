using CSharpFunctionalExtensions;
using Domain.Entitys.AlertTemplates.Dto;

namespace Domain.Entitys.AlertTemplates.Interfaces;

public interface IAlertDatasetScriptExecutor
{
    Result<AlertDatasetResult> Execute(string script, AlertDatasetContext context);
}
