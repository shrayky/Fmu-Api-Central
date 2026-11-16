using Application.Etrn;
using Application.Vetis;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Etrn;
using Domain.Etrn.Xml;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.SalePoints;

/// <summary>
/// Ручное сопоставление точки с входящим документом и проверка ссылок перед удалением точки.
/// Имя и «закрыта» на документ не копируются: их подставляет чтение из карточки организации.
/// </summary>
[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class SalePointService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IEtrnIncomingRepository _etrn;
    private readonly IVetisIncomingRepository _vetis;
    private readonly EtrnIncomingQueryService _etrnQuery;
    private readonly VetisIncomingQueryService _vetisQuery;

    public SalePointService(
        IOrganizationRepository organizations,
        IEtrnIncomingRepository etrn,
        IVetisIncomingRepository vetis,
        EtrnIncomingQueryService etrnQuery,
        VetisIncomingQueryService vetisQuery)
    {
        _organizations = organizations;
        _etrn = etrn;
        _vetis = vetis;
        _etrnQuery = etrnQuery;
        _vetisQuery = vetisQuery;
    }

    public async Task<Result<EtrnIncomingView>> AssignEtrnAsync(string documentId, string salePointUid)
    {
        var found = await _etrn.Find(documentId);
        if (found.IsFailure)
            return Result.Failure<EtrnIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<EtrnIncomingView>("Документ не найден.");

        var document = found.Value;
        var point = await CheckPoint(document.OrganizationId, salePointUid);
        if (point.IsFailure)
            return Result.Failure<EtrnIncomingView>(point.Error);

        document.SalePointUid = point.Value;
        var saved = await _etrn.Save(document);
        if (saved.IsFailure)
            return Result.Failure<EtrnIncomingView>(saved.Error);

        return await _etrnQuery.GetAsync(document.Id);
    }

    public async Task<Result<VetisIncomingView>> AssignVetisAsync(string documentId, string salePointUid)
    {
        var found = await _vetis.Find(documentId);
        if (found.IsFailure)
            return Result.Failure<VetisIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<VetisIncomingView>("Документ не найден.");

        var document = found.Value;
        var point = await CheckPoint(document.OrganizationId, salePointUid);
        if (point.IsFailure)
            return Result.Failure<VetisIncomingView>(point.Error);

        document.SalePointUid = point.Value;
        var saved = await _vetis.Save(document);
        if (saved.IsFailure)
            return Result.Failure<VetisIncomingView>(saved.Error);

        return await _vetisQuery.GetAsync(document.Id);
    }

    /// <summary>
    /// Подбор точки по адресу при открытии карточки: заполняется только пустое поле.
    /// </summary>
    public async Task<Result<EtrnIncomingView>> MatchEtrnAsync(string documentId)
    {
        var found = await _etrn.Find(documentId);
        if (found.IsFailure)
            return Result.Failure<EtrnIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<EtrnIncomingView>("Документ не найден.");

        var document = found.Value;
        if (document.SalePointUid.Length > 0)
            return await _etrnQuery.GetAsync(document.Id);

        var organization = await _organizations.GetById(document.OrganizationId);
        if (organization.IsFailure)
            return Result.Failure<EtrnIncomingView>(organization.Error);

        var uid = SalePointMatch.Find(organization.Value.SalePoints, EtrnAddressOf(document));
        if (uid.Length == 0)
            return await _etrnQuery.GetAsync(document.Id);

        document.SalePointUid = uid;
        var saved = await _etrn.Save(document);
        if (saved.IsFailure)
            return Result.Failure<EtrnIncomingView>(saved.Error);

        return await _etrnQuery.GetAsync(document.Id);
    }

    /// <summary>
    /// Подбор точки по адресу площадки: у ВСД своего адреса нет, он берётся из карточки предприятия.
    /// </summary>
    public async Task<Result<VetisIncomingView>> MatchVetisAsync(string documentId)
    {
        var found = await _vetis.Find(documentId);
        if (found.IsFailure)
            return Result.Failure<VetisIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<VetisIncomingView>("Документ не найден.");

        var document = found.Value;
        if (document.SalePointUid.Length > 0)
            return await _vetisQuery.GetAsync(document.Id);

        var organization = await _organizations.GetById(document.OrganizationId);
        if (organization.IsFailure)
            return Result.Failure<VetisIncomingView>(organization.Error);

        var uid = SalePointMatch.Find(
            organization.Value.SalePoints,
            EnterpriseAddress(organization.Value, document.EnterpriseGuid));
        if (uid.Length == 0)
            return await _vetisQuery.GetAsync(document.Id);

        document.SalePointUid = uid;
        var saved = await _vetis.Save(document);
        if (saved.IsFailure)
            return Result.Failure<VetisIncomingView>(saved.Error);

        return await _vetisQuery.GetAsync(document.Id);
    }

    /// <summary>
    /// Адрес доставки есть не у всех старых карточек: титул читается, если поле ещё пустое.
    /// </summary>
    private static string EtrnAddressOf(EtrnIncomingDocumentEntity document)
    {
        if (!string.IsNullOrWhiteSpace(document.DeliveryAddress))
            return document.DeliveryAddress;

        if (string.IsNullOrWhiteSpace(document.RawXml))
            return "";

        var title = SenderTitleReader.Read(document.RawXml);
        return title.IsSuccess ? title.Value.Address : "";
    }

    /// <summary>
    /// Подтверждённое добавление адреса документа в синонимы выбранной точки.
    /// </summary>
    public async Task<Result<EtrnIncomingView>> RememberEtrnAliasAsync(string documentId)
    {
        var found = await _etrn.Find(documentId);
        if (found.IsFailure)
            return Result.Failure<EtrnIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<EtrnIncomingView>("Документ не найден.");

        var document = found.Value;
        if (document.SalePointUid.Length > 0)
        {
            var remembered = await RememberAlias(
                document.OrganizationId,
                document.SalePointUid,
                _ => EtrnAddressOf(document));
            if (remembered.IsFailure)
                return Result.Failure<EtrnIncomingView>(remembered.Error);
        }

        return await _etrnQuery.GetAsync(document.Id);
    }

    public async Task<Result<VetisIncomingView>> RememberVetisAliasAsync(string documentId)
    {
        var found = await _vetis.Find(documentId);
        if (found.IsFailure)
            return Result.Failure<VetisIncomingView>(found.Error);

        if (found.Value == null)
            return Result.Failure<VetisIncomingView>("Документ не найден.");

        var document = found.Value;
        if (document.SalePointUid.Length > 0)
        {
            var remembered = await RememberAlias(
                document.OrganizationId,
                document.SalePointUid,
                organization => EnterpriseAddress(organization, document.EnterpriseGuid));
            if (remembered.IsFailure)
                return Result.Failure<VetisIncomingView>(remembered.Error);
        }

        return await _vetisQuery.GetAsync(document.Id);
    }

    /// <summary>
    /// Ручной выбор запоминает написание адреса поставщика синонимом: следующий документ подберётся сам.
    /// Чужой адрес другой открытой точки не перетягивается, иначе справочник станет неоднозначным.
    /// </summary>
    private async Task<Result> RememberAlias(
        string organizationId,
        string salePointUid,
        Func<OrganizationEntity, string> addressOf)
    {
        if (salePointUid.Length == 0)
            return Result.Success();

        var found = await _organizations.GetById(organizationId);
        if (found.IsFailure)
            return Result.Failure(found.Error);

        var organization = found.Value;
        var text = (addressOf(organization) ?? "").Trim();
        if (text.Length == 0)
            return Result.Failure("У документа нет адреса");

        if (text.Length > 250)
            return Result.Failure("Адрес синонима: не больше 250 символов");

        var points = organization.SalePoints ?? [];
        var point = points.FirstOrDefault(item =>
            string.Equals(item.Uid, salePointUid, StringComparison.OrdinalIgnoreCase));
        if (point == null)
            return Result.Failure("Точка не найдена в справочнике организации");

        if (point.Closed)
            return Result.Failure("Точка закрыта: выберите открытую");

        if (Matches(point, text))
            return Result.Failure($"Адрес уже указан у точки «{point.Name}»");

        var other = points.FirstOrDefault(item =>
            !item.Closed
            && !string.Equals(item.Uid, point.Uid, StringComparison.OrdinalIgnoreCase)
            && Matches(item, text));
        if (other != null)
            return Result.Failure($"Адрес «{text}» уже указан у открытой точки «{other.Name}»");

        point.Aliases ??= [];
        point.Aliases.Add(new SalePointAlias { Uid = Guid.NewGuid().ToString(), Address = text });
        return await _organizations.Update(organization);
    }

    private static bool Matches(SalePoint point, string address)
    {
        if (string.Equals((point.Address ?? "").Trim(), address, StringComparison.OrdinalIgnoreCase))
            return true;

        return (point.Aliases ?? []).Any(alias =>
            string.Equals((alias.Address ?? "").Trim(), address, StringComparison.OrdinalIgnoreCase));
    }

    private static string EnterpriseAddress(OrganizationEntity organization, string enterpriseGuid) =>
        (organization.VetisSettings?.Enterprises ?? [])
            .FirstOrDefault(item => string.Equals(item.Guid, enterpriseGuid, StringComparison.OrdinalIgnoreCase))
            ?.Address ?? "";

    /// <summary>
    /// Пустая строка — очистка. Иначе точка обязана быть в справочнике организации и быть открытой.
    /// </summary>
    private async Task<Result<string>> CheckPoint(string organizationId, string salePointUid)
    {
        var text = (salePointUid ?? "").Trim();
        if (text.Length == 0)
            return Result.Success("");

        var organization = await _organizations.GetById(organizationId);
        if (organization.IsFailure)
            return Result.Failure<string>(organization.Error);

        var point = new SalePointDirectory([organization.Value]).Find(organizationId, text);
        if (point == null)
            return Result.Failure<string>("Точка не найдена в справочнике организации");

        if (point.Closed)
            return Result.Failure<string>("Точка закрыта: выберите открытую");

        return Result.Success(point.Uid);
    }
}
