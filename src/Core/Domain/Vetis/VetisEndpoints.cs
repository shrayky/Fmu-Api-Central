namespace Domain.Vetis;

public static class VetisEndpoints
{
    public const string ProductionOrigin = "https://api.vetrf.ru";

    public const string ProductionApplicationManagement =
        ProductionOrigin + "/platform/services/2.1/ApplicationManagementService";

    public const string ServiceId = "mercury-g2b.service:2.1";

    /// <summary>
    /// В настройке хранится площадка. Путь и версия сервиса от неё не зависят.
    /// </summary>
    public static string Origin(string? configured)
    {
        var endpoint = (configured ?? "").Trim();
        if (endpoint.Length == 0)
            return ProductionOrigin;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return endpoint;

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static string Resolve(string? configured) =>
        Service(configured, "ApplicationManagementService");

    public static bool Accept(string? configured)
    {
        var endpoint = (configured ?? "").Trim();
        if (endpoint.Length == 0)
            return true;

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Список площадок на той же площадке, что и заявки.
    /// </summary>
    public static string EnterpriseService(string applicationManagementEndpoint) =>
        Service(applicationManagementEndpoint, "EnterpriseService");

    private static string Service(string? configured, string name)
    {
        var origin = Origin(configured);
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (configured ?? "").Trim();

        return origin + "/platform/services/2.1/" + name;
    }
}
