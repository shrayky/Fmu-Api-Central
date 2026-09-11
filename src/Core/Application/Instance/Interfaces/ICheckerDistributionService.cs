using CSharpFunctionalExtensions;
using Domain.Entitys.Instance.Dto;

namespace Application.Instance.Interfaces;

public interface ICheckerDistributionService
{
    Task<Result<CheckerDistributionDownload>> Build(string token);
}
