namespace Domain.Entitys.Organization;

/// <summary>
/// Чтение справочника точек по карточкам организаций: имя и «закрыта» подставляются при выдаче,
/// поэтому переименование точки не требует правки документов.
/// </summary>
public sealed class SalePointDirectory
{
    private readonly Dictionary<string, OrganizationEntity> _organizations;

    public SalePointDirectory(IEnumerable<OrganizationEntity> organizations) =>
        _organizations = organizations.ToDictionary(item => item.Id, StringComparer.Ordinal);

    public SalePoint? Find(string organizationId, string uid)
    {
        if (!_organizations.TryGetValue(organizationId ?? "", out var organization))
            return null;

        return (organization.SalePoints ?? []).FirstOrDefault(point =>
            string.Equals(point.Uid, uid ?? "", StringComparison.OrdinalIgnoreCase));
    }

    public string Name(string organizationId, string uid) => Find(organizationId, uid)?.Name ?? "";

    public bool Closed(string organizationId, string uid) => Find(organizationId, uid)?.Closed ?? false;

    public IReadOnlyList<SalePointOption> Options(string organizationId, bool openOnly)
    {
        if (!_organizations.TryGetValue(organizationId ?? "", out var organization))
            return [];

        return (organization.SalePoints ?? [])
            .Where(point => !openOnly || !point.Closed)
            .Select(point => new SalePointOption
            {
                Uid = point.Uid,
                Name = point.Name,
                Closed = point.Closed
            })
            .ToList();
    }

    /// <summary>
    /// Две открытые точки не должны делить адрес или синоним. Возвращается точка, у которой адрес уже есть,
    /// и повторная запись — по её признаку выбирается слово в сообщении.
    /// </summary>
    public static (SalePoint Point, string Address, bool Alias)? Collision(IEnumerable<SalePoint>? points)
    {
        var seen = new Dictionary<string, SalePoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var point in points ?? [])
        {
            if (point.Closed)
                continue;

            foreach (var entry in Entries(point))
            {
                var address = Normalize(entry.Address);
                if (address.Length == 0)
                    continue;

                if (seen.TryGetValue(address, out var owner) && !ReferenceEquals(owner, point))
                    return (owner, entry.Address, entry.Alias);

                seen.TryAdd(address, point);
            }
        }

        return null;
    }

    private static IEnumerable<(string Address, bool Alias)> Entries(SalePoint point)
    {
        yield return (point.Address, false);
        foreach (var alias in point.Aliases ?? [])
            yield return (alias.Address, true);
    }

    private static string Normalize(string? address) => (address ?? "").Trim();
}
