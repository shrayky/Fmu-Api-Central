using CSharpFunctionalExtensions;

namespace Domain.Vetis;

public sealed class VetisIncomingFilter
{
    public string OrganizationId { get; init; } = "";

    public string EnterpriseGuid { get; init; } = "";

    public string Status { get; init; } = "";
}

public interface IVetisIncomingRepository
{
    Task<Result<VetisIncomingDocumentEntity?>> Find(string id);

    Task<Result> Save(VetisIncomingDocumentEntity document);

    /// <summary>
    /// Убирает uuid из других документов площадки. keep — документы текущей страницы ВетИС, их состав не трогаем.
    /// </summary>
    Task<Result> Release(string organizationId, string enterpriseGuid, IReadOnlyCollection<string> uuids, IReadOnlyCollection<string> keepDocumentIds);

    /// <summary>
    /// Строки списка приходят без XML ВСД: нужны только счётчики документа.
    /// </summary>
    Task<Result<List<VetisIncomingDocumentEntity>>> List(VetisIncomingFilter filter, int skip, int limit);
}
