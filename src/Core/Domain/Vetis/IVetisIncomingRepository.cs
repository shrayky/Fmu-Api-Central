using CSharpFunctionalExtensions;

namespace Domain.Vetis;

public sealed class VetisIncomingFilter
{
    public string OrganizationId { get; init; } = "";

    public string EnterpriseGuid { get; init; } = "";

    public string Status { get; init; } = "";
}

/// <summary>
/// Строка обхода без ВСД и XML: для выравнивания ключа и отбора чужого TRANSPORT большего не нужно.
/// </summary>
public readonly record struct VetisIncomingHead(string Id, string Date, long SortAt);

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

    /// <summary>
    /// Пары организация-площадка документов «Не принят», без XML ВСД.
    /// </summary>
    Task<Result<List<VetisIncomingKey>>> NotAcceptedKeys();

    /// <summary>
    /// Страница журнала по возрастанию id: после выравнивания у многих документов один ключ, и skip по нему дублирует строки.
    /// </summary>
    Task<Result<List<VetisIncomingHead>>> HeadsAfter(string afterId, int limit);

    Task<Result> Delete(string id);
}
