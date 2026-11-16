using CSharpFunctionalExtensions;

namespace Domain.Entitys.Organization.Interfaces;

/// <summary>
/// Ссылки на точку в журналах: по ним удаление точки из справочника запрещается.
/// </summary>
public interface ISalePointUsage
{
    Task<Result<bool>> UsedAsync(string organizationId, string salePointUid);
}
