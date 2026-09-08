using System.Net;

namespace Domain.Configuration;

public static class TrustedProxies
{
    public static IPAddress[] Resolve(IEnumerable<string>? values)
    {
        var proxies = new HashSet<IPAddress>();
        if (values == null)
            return [];

        foreach (var item in values)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            if (IPAddress.TryParse(item.Trim(), out var ip))
                proxies.Add(ip);
        }

        return [.. proxies];
    }
}
