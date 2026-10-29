namespace Domain.Vetis;

public static class VetisEndpoints
{
    public const string ProductionApplicationManagement =
        "https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService";

    public const string ServiceId = "mercury-g2b.service:3.0";

    public static string Resolve(string? configured)
    {
        var endpoint = (configured ?? "").Trim();
        return endpoint.Length == 0 ? ProductionApplicationManagement : endpoint;
    }

    public static bool Accept(string? configured)
    {
        var endpoint = (configured ?? "").Trim();
        if (endpoint.Length == 0)
            return true;

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Та же база URL: ApplicationManagementService заменяется на EnterpriseService.
    /// </summary>
    public static string EnterpriseService(string applicationManagementEndpoint)
    {
        var endpoint = (applicationManagementEndpoint ?? "").Trim().TrimEnd('/');
        const string marker = "ApplicationManagementService";
        var index = endpoint.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return string.Concat(
                endpoint.AsSpan(0, index),
                "EnterpriseService",
                endpoint.AsSpan(index + marker.Length));
        }

        var slash = endpoint.LastIndexOf('/');
        return slash < 0
            ? endpoint + "/EnterpriseService"
            : endpoint[..(slash + 1)] + "EnterpriseService";
    }
}
