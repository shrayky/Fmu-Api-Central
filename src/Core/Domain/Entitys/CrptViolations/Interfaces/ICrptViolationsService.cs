using Domain.Entitys.CrptViolations.Dto;

namespace Domain.Entitys.CrptViolations.Interfaces;

public interface ICrptViolationsService
{
    Task<CrptViolationsDashboard> List(int pageNumber, int pageSize, CrptViolationsListFilter filter);
}
