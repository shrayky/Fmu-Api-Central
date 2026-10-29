using Domain.Entitys.Organization;

namespace Application.Vetis;

/// <summary>
/// Названия организаций и площадок для строк списков. Берутся из карточек, а не из документов: их можно переименовать.
/// </summary>
internal sealed class VetisDirectory
{
    private readonly Dictionary<string, OrganizationEntity> _organizations;

    public VetisDirectory(IEnumerable<OrganizationEntity> organizations)
    {
        _organizations = organizations.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public string OrganizationName(string organizationId) =>
        _organizations.TryGetValue(organizationId, out var organization) ? organization.Name : "";

    public string EnterpriseName(string organizationId, string enterpriseGuid)
    {
        if (!_organizations.TryGetValue(organizationId, out var organization))
            return "";

        var enterprise = organization.VetisSettings.Enterprises
            .FirstOrDefault(item => string.Equals(item.Guid, enterpriseGuid, StringComparison.OrdinalIgnoreCase));
        return enterprise == null ? "" : Caption(enterprise);
    }

    public static string Caption(OrganizationEnterprise enterprise)
    {
        if (!string.IsNullOrWhiteSpace(enterprise.Presentation))
            return enterprise.Presentation;

        return string.IsNullOrWhiteSpace(enterprise.Name) ? enterprise.Guid : enterprise.Name;
    }
}
