namespace Domain.Entitys.Organization;

/// <summary>
/// Подбор точки по адресу доставки. Сравнение — обрезка пробелов по краям и без учёта регистра:
/// внутренние пробелы и знаки препинания не нормализуются, расхождения закрывает синоним.
/// </summary>
public static class SalePointMatch
{
    public static string Find(IEnumerable<SalePoint>? points, string address)
    {
        var text = (address ?? "").Trim();
        if (text.Length == 0 || points == null)
            return "";

        var open = points.Where(point => !point.Closed).ToList();

        // Адрес важнее синонима: точное совпадение не должно проигрывать чужому варианту написания.
        var byAddress = open.Where(point => Equal(point.Address, text)).ToList();
        if (byAddress.Count > 0)
            return byAddress.Count == 1 ? byAddress[0].Uid : "";

        var byAlias = open
            .Where(point => (point.Aliases ?? []).Any(alias => Equal(alias.Address, text)))
            .ToList();
        return byAlias.Count == 1 ? byAlias[0].Uid : "";
    }

    private static bool Equal(string? value, string address) =>
        string.Equals((value ?? "").Trim(), address, StringComparison.OrdinalIgnoreCase);
}
