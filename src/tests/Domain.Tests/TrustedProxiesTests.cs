using System.Net;
using Domain.Configuration;

namespace Domain.Tests;

public class TrustedProxiesTests
{
    /// <summary>
    /// Пустой список — доверяем только loopback ASP.NET, не всю LAN.
    /// </summary>
    [Fact]
    public void Resolve_пустой_ничего_не_добавляет()
    {
        Assert.Empty(TrustedProxies.Resolve(null));
        Assert.Empty(TrustedProxies.Resolve(["", "  "]));
    }

    /// <summary>
    /// Валидные IPv4/IPv6 попадают, мусор отбрасывается.
    /// </summary>
    [Fact]
    public void Resolve_принимает_только_IP()
    {
        var proxies = TrustedProxies.Resolve(
        [
            " 192.168.1.5 ",
            "not-an-ip",
            "::1",
            "http://192.168.1.5"
        ]);

        Assert.Equal(2, proxies.Length);
        Assert.Contains(IPAddress.Parse("192.168.1.5"), proxies);
        Assert.Contains(IPAddress.IPv6Loopback, proxies);
    }
}
