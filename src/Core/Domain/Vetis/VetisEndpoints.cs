namespace Domain.Vetis;

public static class VetisEndpoints
{
    public const string ProductionApplicationManagement =
        "https://api.vetrf.ru/platform/services/2.1/ApplicationManagementService";

    public const string ServiceId = "mercury-g2b.service:2.1";

    public static string Resolve(string? configured)
    {
        var endpoint = (configured ?? "").Trim();
        if (endpoint.Length == 0)
            return ProductionApplicationManagement;

        // Сохранённый адрес опытной 3.0 не должен оставлять вызовы на ней.
        return endpoint.Replace("/services/3.0/", "/services/2.1/", StringComparison.OrdinalIgnoreCase);
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
