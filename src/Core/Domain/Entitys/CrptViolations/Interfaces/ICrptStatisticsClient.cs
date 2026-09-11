using CSharpFunctionalExtensions;

namespace Domain.Entitys.CrptViolations.Interfaces;

public interface ICrptStatisticsClient
{
    Task<Result<string>> FetchViolations(string inn, string token, DateOnly date, CancellationToken cancellationToken);
    Task<Result<string>> FetchPenalty(string inn, string token, DateOnly date, CancellationToken cancellationToken);
}
