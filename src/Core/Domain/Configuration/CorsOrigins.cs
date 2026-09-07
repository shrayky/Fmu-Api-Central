namespace Domain.Configuration;

public static class CorsOrigins
{
    public const string Localhost = "http://localhost:2580";
    public const string Loopback = "http://127.0.0.1:2580";

    public static string[] Resolve(IEnumerable<string>? extra)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Localhost,
            Loopback
        };

        if (extra == null)
            return [.. origins];

        foreach (var item in extra)
        {
            var origin = Normalize(item);
            if (origin != null)
                origins.Add(origin);
        }

        return [.. origins];
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
