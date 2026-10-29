using System.Globalization;
using Domain.Configuration;
using Domain.Entitys.Organization;

namespace Domain.Vetis;

public static class OrganizationVetisScope
{
    public static void Apply(OrganizationVetisSettings current, OrganizationVetisSettings update)
    {
        current.Enable = update.Enable;
        var apiKey = (update.ApiKey ?? "").Trim();
        if (apiKey.Length > 0 && !SecretMask.IsPlaceholder(apiKey))
            current.ApiKey = apiKey;
        current.BasicLogin = (update.BasicLogin ?? "").Trim();
        var password = update.BasicPassword ?? "";
        if (password.Length > 0 && !SecretMask.IsPlaceholder(password))
            current.BasicPassword = password;

        current.IssuerId = (update.IssuerId ?? "").Trim();
        current.InitiatorLogin = (update.InitiatorLogin ?? "").Trim();
        current.IncomingFrom = NormalizeDate(update.IncomingFrom);
        current.Enterprises = Normalize(update.Enterprises, current.IncomingFrom);
    }

    public static string NormalizeDate(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0)
            return "";

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return text;

        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return "";
    }

    private static List<OrganizationEnterprise> Normalize(IReadOnlyList<OrganizationEnterprise>? source, string generalDate)
    {
        var result = new List<OrganizationEnterprise>();
        foreach (var item in source ?? [])
        {
            var guid = (item.Guid ?? "").Trim();
            if (guid.Length == 0 || result.Exists(existing => string.Equals(existing.Guid, guid, StringComparison.OrdinalIgnoreCase)))
                continue;

            var incoming = NormalizeDate(item.IncomingFrom);
            if (item.Participates && incoming.Length == 0 && generalDate.Length > 0)
                incoming = generalDate;

            result.Add(new OrganizationEnterprise
            {
                Guid = guid,
                Name = (item.Name ?? "").Trim(),
                Address = (item.Address ?? "").Trim(),
                Presentation = (item.Presentation ?? "").Trim(),
                IncomingFrom = incoming,
                Participates = item.Participates
            });
        }

        FillPresentations(result);
        return result;
    }

    public static List<OrganizationEnterprise> Merge(
        IReadOnlyList<OrganizationEnterprise>? stored,
        IReadOnlyList<VetisEnterprise>? fresh)
    {
        var known = Copy(stored);
        var result = new List<OrganizationEnterprise>();
        foreach (var item in fresh ?? [])
        {
            var guid = (item.Guid ?? "").Trim();
            if (guid.Length == 0 || result.Exists(existing => string.Equals(existing.Guid, guid, StringComparison.OrdinalIgnoreCase)))
                continue;

            var previous = known.Find(existing => string.Equals(existing.Guid, guid, StringComparison.OrdinalIgnoreCase));
            result.Add(new OrganizationEnterprise
            {
                Guid = guid,
                Name = First(item.Name, previous?.Name),
                Address = First(item.Address, previous?.Address),
                Presentation = previous?.Presentation ?? "",
                IncomingFrom = previous?.IncomingFrom ?? "",
                Participates = previous?.Participates ?? false
            });
        }

        foreach (var item in known)
        {
            if (result.Exists(existing => string.Equals(existing.Guid, item.Guid, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(item);
        }

        FillPresentations(result);
        return result;
    }

    private static List<OrganizationEnterprise> Copy(IReadOnlyList<OrganizationEnterprise>? source)
    {
        var result = new List<OrganizationEnterprise>();
        foreach (var item in source ?? [])
        {
            var guid = (item.Guid ?? "").Trim();
            if (guid.Length == 0 || result.Exists(existing => string.Equals(existing.Guid, guid, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(new OrganizationEnterprise
            {
                Guid = guid,
                Name = (item.Name ?? "").Trim(),
                Address = (item.Address ?? "").Trim(),
                Presentation = (item.Presentation ?? "").Trim(),
                IncomingFrom = NormalizeDate(item.IncomingFrom),
                Participates = item.Participates
            });
        }

        return result;
    }

    private static string First(string? primary, string? fallback)
    {
        var value = (primary ?? "").Trim();
        return value.Length == 0 ? (fallback ?? "").Trim() : value;
    }

    private static void FillPresentations(List<OrganizationEnterprise> enterprises)
    {
        foreach (var item in enterprises)
        {
            if (!IsGenerated(item))
                continue;

            item.Presentation = Caption(item.Name, item.Address);
        }
    }

    /// <summary>
    /// Пустое и прежнее автопредставление (имя или имя с полным адресом) можно пересобрать.
    /// </summary>
    private static bool IsGenerated(OrganizationEnterprise item)
    {
        var presentation = item.Presentation;
        if (presentation.Length == 0)
            return true;

        var name = item.Name;
        var address = item.Address;
        if (address.Length > 0 && presentation.Equals(address, StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.Length == 0)
            return false;

        if (address.Length > 0 && presentation.Equals(name, StringComparison.OrdinalIgnoreCase))
            return true;

        if (address.Length > 0 && presentation.Equals(name + ", " + address, StringComparison.OrdinalIgnoreCase))
            return true;

        var street = ShortStreet(address);
        return street.Length > 0
            && presentation.Equals(name + ", " + street, StringComparison.OrdinalIgnoreCase);
    }

    private static string Caption(string name, string address)
    {
        var street = ShortStreet(address);
        if (street.Length > 0)
            return street;

        if (name.Length > 0)
            return name;

        if (address.Length > 0)
            return address;

        return "Без названия";
    }

    /// <summary>
    /// addressView Ветис идёт от страны к дому. Список показывает кусок с типом улицы.
    /// </summary>
    private static string ShortStreet(string address)
    {
        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (HasStreetToken(parts[i]))
                return string.Join(", ", parts[i..]);
        }

        return "";
    }

    private static bool HasStreetToken(string part)
    {
        foreach (var token in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = token.Trim().TrimEnd('.').ToLowerInvariant();
            if (StreetTokens.Contains(normalized))
                return true;
        }

        return false;
    }

    private static readonly HashSet<string> StreetTokens = new(StringComparer.Ordinal)
    {
        "ул", "улица", "пр-кт", "пр-т", "проспект", "пр", "пер", "переулок",
        "б-р", "бульвар", "ш", "шоссе", "наб", "набережная", "проезд",
        "туп", "тупик", "пл", "площадь", "аллея", "тракт", "линия", "мкр", "микрорайон", "км"
    };
}
