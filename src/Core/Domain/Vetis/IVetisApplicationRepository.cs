using CSharpFunctionalExtensions;

namespace Domain.Vetis;

public sealed class VetisApplicationFilter
{
    public string OrganizationId { get; init; } = "";

    public string EnterpriseGuid { get; init; } = "";
}

public interface IVetisApplicationRepository
{
    Task<Result> Save(VetisApplicationEntity application);

    Task<Result<VetisApplicationEntity?>> Find(string id);

    /// <summary>
    /// Строки списка приходят без XML запроса и ответа.
    /// </summary>
    Task<Result<List<VetisApplicationEntity>>> List(VetisApplicationFilter filter, int skip, int limit);

    Task<Result<List<VetisApplicationEntity>>> ListExpired(bool completed, long createdBefore, int limit);

    Task<Result> Delete(IReadOnlyCollection<string> ids);
}
