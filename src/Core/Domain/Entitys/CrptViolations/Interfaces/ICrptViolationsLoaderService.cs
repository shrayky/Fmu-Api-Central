using CSharpFunctionalExtensions;

namespace Domain.Entitys.CrptViolations.Interfaces;

public interface ICrptViolationsLoaderService
{
    Task<Result> Load(DateOnly today, CancellationToken cancellationToken, string? inn = null);
}
