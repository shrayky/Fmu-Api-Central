using Domain.Entitys.Organization;

namespace Domain.Entitys.CrptViolations;

public static class CrptViolationsLoadRules
{
    /// <summary>
    /// Организация участвует в загрузке, если включена интеграция и выбран сертификат ЭЦП.
    /// </summary>
    public static bool CanLoad(OrganizationEntity organization)
    {
        if (string.IsNullOrWhiteSpace(organization.Inn))
            return false;

        var settings = organization.TrueApiIntegrationSettings;
        if (settings is null || !settings.Enable)
            return false;

        return !string.IsNullOrWhiteSpace(settings.DigitalSignature);
    }

    /// <summary>
    /// Первое число месяца для фильтра ЧЗ и поиска сохранённых дней.
    /// </summary>
    public static DateOnly MonthStart(DateOnly day)
        => new(day.Year, day.Month, 1);

    /// <summary>
    /// Последний сохранённый день месяца до сегодня — база для расчёта прироста.
    /// </summary>
    public static DateOnly? PreviousDate(IReadOnlyCollection<DateOnly> existingDates, DateOnly today)
    {
        DateOnly? previous = null;

        foreach (var date in existingDates)
        {
            if (date >= today)
                continue;

            if (previous is null || date > previous)
                previous = date;
        }

        return previous;
    }

    /// <summary>
    /// Идентификатор документа CouchDB: ИНН и дата дня.
    /// </summary>
    public static string DocumentId(string inn, DateOnly date)
        => $"{inn}_{date:yyyyMMdd}";
}
