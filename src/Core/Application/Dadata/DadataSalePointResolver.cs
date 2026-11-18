using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Dadata;

/// <summary>
/// Подбор торговой точки по ФИАС дома, полученному стандартизацией DaData.
/// Точка пишется документу, исходный адрес документа — ей синонимом.
/// </summary>
[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class DadataSalePointResolver
{
    private readonly DadataAddressService _dadata;
    private readonly IOrganizationRepository _organizations;
    private readonly ILogger<DadataSalePointResolver> _logger;

    public DadataSalePointResolver(
        DadataAddressService dadata,
        IOrganizationRepository organizations,
        ILogger<DadataSalePointResolver> logger)
    {
        _dadata = dadata;
        _organizations = organizations;
        _logger = logger;
    }

    /// <summary>
    /// Uid единственной открытой точки с этим ФИАС (или с этой канонической строкой у точки без ФИАС).
    /// Ошибка DaData и лишние совпадения оставляют документ без точки.
    /// </summary>
    public async Task<string> Resolve(
        OrganizationEntity organization,
        string address,
        DadataLookupCache cache,
        CancellationToken cancellationToken)
    {
        var text = (address ?? "").Trim();
        if (text.Length == 0)
            return "";

        if (!cache.TryGet(text, out var outcome))
        {
            var cleaned = await _dadata.Clean(text, cancellationToken);
            outcome = cleaned.IsFailure
                ? Result.Failure<DadataCleanOutcome>(cleaned.Error)
                : Result.Success(cleaned.Value);
            cache.Remember(text, outcome);

            if (cleaned.IsFailure)
                _logger.LogWarning("DaData не разобрала адрес «{Address}»: {Error}", text, cleaned.Error);
        }

        if (outcome.IsFailure || !outcome.Value.Confident)
            return "";

        var points = MatchByFias(organization, outcome.Value.FiasId);
        if (points.Count == 0)
            points = MatchByStandardAddress(organization, outcome.Value.Address);
        if (points.Count != 1)
            return "";

        var uid = points[0].Uid;

        // Перед записью карточка читается заново: точка могла исчезнуть за время загрузки.
        var fresh = await _organizations.GetById(organization.Id);
        if (fresh.IsFailure)
        {
            _logger.LogWarning(
                "Организация {Organization} не прочитана для записи точки: {Error}",
                organization.Id,
                fresh.Error);
            return "";
        }

        var point = (fresh.Value.SalePoints ?? []).FirstOrDefault(item =>
            !item.Closed && string.Equals(item.Uid, uid, StringComparison.OrdinalIgnoreCase));
        if (point == null)
            return "";

        var alias = SalePointAliasRules.Add(fresh.Value, uid, text);
        if (alias.IsFailure)
        {
            _logger.LogInformation(
                "Точка {Point} подобрана по ФИАС, синоним не записан: {Reason}",
                uid,
                alias.Error);
            return uid;
        }

        var saved = await _organizations.Update(fresh.Value);
        if (saved.IsFailure)
        {
            _logger.LogWarning("Синоним точки {Point} не сохранён: {Error}", uid, saved.Error);
            return uid;
        }

        // Второй документ прохода должен совпасть по синониму, не доходя до DaData.
        SalePointAliasRules.Add(organization, uid, text);
        return uid;
    }

    private static List<SalePoint> MatchByFias(OrganizationEntity organization, string fiasId) =>
        (organization.SalePoints ?? [])
            .Where(point => !point.Closed)
            .Where(point => string.Equals((point.FiasId ?? "").Trim(), fiasId, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Запасной поиск для точек без ФИАС. result — каноническая строка DaData, не адрес накладной,
    /// поэтому совпадение до вызова DaData её не закрывает. Сравнение как у синонимов: обрезка пробелов
    /// и без учёта регистра. Точки с заполненным ФИАС сюда не попадают.
    /// </summary>
    private static List<SalePoint> MatchByStandardAddress(OrganizationEntity organization, string standardAddress)
    {
        var text = (standardAddress ?? "").Trim();
        if (text.Length == 0)
            return [];

        return (organization.SalePoints ?? [])
            .Where(point => !point.Closed)
            .Where(point => (point.FiasId ?? "").Trim().Length == 0)
            .Where(point => SalePointAliasRules.Matches(point, text))
            .ToList();
    }
}
