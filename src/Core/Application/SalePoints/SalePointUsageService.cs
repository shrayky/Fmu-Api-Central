using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.Organization.Interfaces;
using Domain.Etrn;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.SalePoints;

/// <summary>
/// Ссылки на точку в обоих журналах. Отдельно от назначения: этой проверкой пользуется и карточка организации.
/// </summary>
[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class SalePointUsageService : ISalePointUsage
{
    private readonly IEtrnIncomingRepository _etrn;
    private readonly IVetisIncomingRepository _vetis;

    public SalePointUsageService(IEtrnIncomingRepository etrn, IVetisIncomingRepository vetis)
    {
        _etrn = etrn;
        _vetis = vetis;
    }

    public async Task<Result<bool>> UsedAsync(string organizationId, string salePointUid)
    {
        var etrn = await _etrn.AnyBySalePoint(organizationId, salePointUid);
        if (etrn.IsFailure)
            return Result.Failure<bool>(etrn.Error);

        if (etrn.Value)
            return Result.Success(true);

        return await _vetis.AnyBySalePoint(organizationId, salePointUid);
    }
}
