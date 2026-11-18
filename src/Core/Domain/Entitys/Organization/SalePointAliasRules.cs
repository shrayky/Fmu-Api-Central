using CSharpFunctionalExtensions;

namespace Domain.Entitys.Organization;

/// <summary>
/// Правило записи синонима адреса: одно и то же для ручного выбора точки и для подбора через DaData.
/// Две открытые точки не должны делить адрес или синоним.
/// </summary>
public static class SalePointAliasRules
{
    public const int MaxAddressLength = 250;

    /// <summary>
    /// Добавляет адрес синонимом открытой точке организации. Точка остаётся в объекте организации: сохранение за вызывающим.
    /// </summary>
    public static Result Add(OrganizationEntity organization, string salePointUid, string address)
    {
        var text = (address ?? "").Trim();
        if (text.Length == 0)
            return Result.Failure("У документа нет адреса");

        if (text.Length > MaxAddressLength)
            return Result.Failure("Адрес синонима: не больше 250 символов");

        var points = organization.SalePoints ?? [];
        var point = points.FirstOrDefault(item =>
            string.Equals(item.Uid, salePointUid ?? "", StringComparison.OrdinalIgnoreCase));
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
        return Result.Success();
    }

    public static bool Matches(SalePoint point, string address)
    {
        if (string.Equals((point.Address ?? "").Trim(), (address ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        return (point.Aliases ?? []).Any(alias =>
            string.Equals((alias.Address ?? "").Trim(), (address ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
